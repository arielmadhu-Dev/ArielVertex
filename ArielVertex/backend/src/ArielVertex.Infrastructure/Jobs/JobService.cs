using System.Globalization;
using ArielVertex.Application.Abstractions;
using ArielVertex.Domain.Enums;
using ArielVertex.Infrastructure.Auth;
using ArielVertex.Infrastructure.Integration;
using ArielVertex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ArielVertex.Infrastructure.Jobs;

/// <summary>
/// Implements the operational automations. Idempotent by design — each run checks for work already
/// done (existing notifications this period, reports already generated) so repeated runs are safe.
/// </summary>
public class JobService : IJobService
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notify;
    private readonly IPerformanceReportService _reports;
    private readonly IntegrationSettings _integration;
    private readonly AzureAdSettings _azure;
    private readonly MicrosoftGraphClient _graph;
    private readonly ITeamsWebhookSender _teams;

    public JobService(AppDbContext db, INotificationService notify, IPerformanceReportService reports,
        IOptions<IntegrationSettings> integration, IOptions<AzureAdSettings> azure,
        MicrosoftGraphClient graph, ITeamsWebhookSender teams)
    { _db = db; _notify = notify; _reports = reports; _integration = integration.Value; _azure = azure.Value; _graph = graph; _teams = teams; }

    // ---- Missing status-update reminders (spec 6.6) ----
    public async Task<JobResult> RunStatusRemindersAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var members = await _db.ProjectMembers.Include(m => m.Project).Include(m => m.User)
            .Where(m => m.IsActive && m.Project!.Status == ProjectStatus.Active &&
                        (m.RoleOnProject == ProjectRole.Developer || m.RoleOnProject == ProjectRole.QA))
            .ToListAsync(ct);

        var reminded = 0;
        foreach (var m in members)
        {
            var reported = await _db.StatusUpdates.AnyAsync(s => s.ProjectId == m.ProjectId && s.UserId == m.UserId && s.UpdateDate >= today, ct);
            if (reported) continue;
            // Don't double-remind the same person on the same day.
            var already = await _db.Notifications.AnyAsync(n => n.RecipientId == m.UserId && n.Type == NotificationType.StatusMissed && n.CreatedAt >= today, ct);
            if (already) continue;

            await _notify.NotifyAsync(m.UserId, NotificationType.StatusMissed, "Status update reminder",
                $"You haven't submitted today's status update for {m.Project!.Name}.", "/status-updates", ct);
            reminded++;
        }

        // Notify PM/PC of projects that have any missing updates today.
        var projectsWithGaps = members
            .Where(m => !_db.StatusUpdates.Any(s => s.ProjectId == m.ProjectId && s.UserId == m.UserId && s.UpdateDate >= today))
            .Select(m => m.ProjectId).Distinct().ToList();
        foreach (var pid in projectsWithGaps)
        {
            var leads = await _db.ProjectMembers.Where(x => x.ProjectId == pid && x.IsActive &&
                (x.RoleOnProject == ProjectRole.ProjectManager || x.RoleOnProject == ProjectRole.ProjectCoordinator)).Select(x => x.UserId).ToListAsync(ct);
            var already = await _db.Notifications.Where(n => leads.Contains(n.RecipientId) && n.Type == NotificationType.StatusMissed && n.CreatedAt >= today).Select(n => n.RecipientId).ToListAsync(ct);
            var targets = leads.Except(already).ToList();
            if (targets.Count > 0)
                await _notify.NotifyManyAsync(targets, NotificationType.StatusMissed, "Team has missing status updates",
                    "One or more team members have not submitted today's update.", "/status-updates", ct);
        }

        return new JobResult("status-reminders", reminded, $"Reminded {reminded} member(s) about missing updates.");
    }

    // ---- Quarter-end feedback requests (spec 6.8) ----
    public async Task<JobResult> RunQuarterlyFeedbackRequestsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var quarter = $"{now.Year}-Q{(now.Month - 1) / 3 + 1}";
        var since = now.AddDays(-30);

        var members = await _db.ProjectMembers.Include(m => m.User).Include(m => m.Project)
            .Where(m => m.IsActive && m.Project!.Status == ProjectStatus.Active &&
                        (m.RoleOnProject == ProjectRole.Developer || m.RoleOnProject == ProjectRole.QA))
            .ToListAsync(ct);

        var requested = 0;
        foreach (var m in members)
        {
            // Reviewer = PM/PC of the project, else the reporting manager.
            var reviewerId = await _db.ProjectMembers.Where(x => x.ProjectId == m.ProjectId && x.IsActive &&
                    (x.RoleOnProject == ProjectRole.ProjectManager || x.RoleOnProject == ProjectRole.ProjectCoordinator))
                .Select(x => (int?)x.UserId).FirstOrDefaultAsync(ct) ?? m.User!.ManagerId;
            if (reviewerId is null) continue;

            // Skip if feedback already exists for this subject+quarter, or a request was recently sent.
            var haveFeedback = await _db.Feedbacks.AnyAsync(f => f.SubjectUserId == m.UserId && f.Period == quarter, ct);
            if (haveFeedback) continue;
            var recentlyAsked = await _db.Notifications.AnyAsync(n => n.RecipientId == reviewerId && n.Type == NotificationType.FeedbackDue && n.CreatedAt >= since && n.Message.Contains(m.User!.Name), ct);
            if (recentlyAsked) continue;

            await _notify.NotifyAsync(reviewerId.Value, NotificationType.FeedbackDue, $"Quarterly feedback due ({quarter})",
                $"Please submit {quarter} feedback for {m.User!.Name}.", "/feedback", ct);
            requested++;
        }
        return new JobResult("quarterly-feedback", requested, $"Requested feedback for {requested} employee(s) for {quarter}.");
    }

    // ---- Monthly report auto-generation (spec 6.9) ----
    public async Task<JobResult> RunMonthlyReportGenerationAsync(CancellationToken ct = default)
    {
        var prev = DateTime.UtcNow.Date.AddDays(-1); // last month relative to a month-start run
        var period = $"{prev.Year:D4}-{prev.Month:D2}";

        // Employees who are active project members (dev/QA) get a monthly draft.
        var userIds = await _db.ProjectMembers.Where(m => m.IsActive &&
                (m.RoleOnProject == ProjectRole.Developer || m.RoleOnProject == ProjectRole.QA))
            .Select(m => m.UserId).Distinct().ToListAsync(ct);

        var generated = 0;
        foreach (var uid in userIds)
        {
            await _reports.GenerateAsync(uid, PerformancePeriodType.Monthly, period, ct); // idempotent upsert
            generated++;
        }

        if (generated > 0)
        {
            var hr = await _db.Users.Where(u => u.Role == PortalRole.HrManager || u.Role == PortalRole.HrDirector).Select(u => u.Id).ToListAsync(ct);
            await _notify.NotifyManyAsync(hr, NotificationType.ReportGenerated, "Monthly reports ready for review",
                $"{generated} draft performance report(s) for {period} are ready to review and publish.", "/performance-reports", ct);
        }
        return new JobResult("monthly-reports", generated, $"Generated {generated} draft report(s) for {period}.");
    }

    // ---- Expense summaries (front desk) ----
    public Task<JobResult> RunDailyExpenseSummaryAsync(CancellationToken ct = default)
        => ExpenseSummaryAsync("Daily", DateTime.UtcNow.Date, s => s.DailySummaryRecipients, ct);

    public Task<JobResult> RunWeeklyExpenseSummaryAsync(CancellationToken ct = default)
        => ExpenseSummaryAsync("Weekly", DateTime.UtcNow.Date.AddDays(-7), s => s.WeeklySummaryRecipients, ct);

    private async Task<JobResult> ExpenseSummaryAsync(string label, DateTime from, Func<Domain.Entities.ExpenseSetting, string> recipients, CancellationToken ct)
    {
        var settings = await _db.ExpenseSettings.FirstOrDefaultAsync(ct);
        if (settings is null) return new JobResult($"{label.ToLower()}-expense-summary", 0, "No expense settings configured.");

        var expenses = await _db.Expenses.Include(e => e.RaisedBy)
            .Where(e => e.CreatedAt >= from).AsNoTracking().ToListAsync(ct);

        var total = expenses.Sum(e => e.Amount);
        var pendingApproval = expenses.Count(e => e.Status == ExpenseStatus.PaymentRequested && e.ApprovalRequired);
        var paid = expenses.Where(e => e.Status == ExpenseStatus.Paid).Sum(e => e.Amount);

        var title = $"{label} Expense Summary — {DateTime.UtcNow:dd MMM yyyy}";
        var lines = expenses.OrderByDescending(e => e.CreatedAt).Take(15)
            .Select(e => $"• {e.Title} — ₹{e.Amount.ToString("N0", CultureInfo.InvariantCulture)} [{e.Status}]");
        var body = $"{expenses.Count} expense(s), total ₹{total.ToString("N0", CultureInfo.InvariantCulture)}. " +
                   $"Paid ₹{paid.ToString("N0", CultureInfo.InvariantCulture)}, {pendingApproval} awaiting approval.\n\n" +
                   string.Join("\n", lines);
        var message = $"{expenses.Count} expense(s), ₹{total.ToString("N0", CultureInfo.InvariantCulture)} total; {pendingApproval} awaiting approval.";

        // Resolve configured recipient emails
        var emails = (recipients(settings) ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant()).Distinct().ToList();

        // Portal notification for recipients who are internal users
        var users = await _db.Users.Where(u => emails.Contains(u.Email)).Select(u => u.Id).ToListAsync(ct);
        if (users.Count > 0)
            await _notify.NotifyManyAsync(users, NotificationType.ReportGenerated, title, message, "/expenses", ct);

        // Outlook email (when Microsoft 365 is live)
        if (_integration.OutlookNotificationsLive && _azure.IsConfigured)
        {
            var html = $"<h3>{title}</h3><p>{System.Net.WebUtility.HtmlEncode(message)}</p><pre style='font-family:inherit'>{System.Net.WebUtility.HtmlEncode(string.Join("\n", lines))}</pre>";
            foreach (var email in emails)
                try { await _graph.SendMailAsync(email, title, html, ct); } catch { /* best-effort */ }
        }

        // Teams channel (when a webhook is configured)
        if (!string.IsNullOrWhiteSpace(settings.TeamsWebhookUrl))
            try { await _teams.SendAsync(settings.TeamsWebhookUrl!, title, body, ct); } catch { /* best-effort */ }

        return new JobResult($"{label.ToLower()}-expense-summary", emails.Count,
            $"{label} summary sent to {emails.Count} recipient(s): {expenses.Count} expenses, ₹{total.ToString("N0", CultureInfo.InvariantCulture)}.");
    }

    // ---- Bill due-date reminders ----
    public async Task<JobResult> RunBillDueRemindersAsync(CancellationToken ct = default)
    {
        var settings = await _db.BillSettings.FirstOrDefaultAsync(ct);
        if (settings is null) return new JobResult("bill-reminders", 0, "No bill settings configured.");

        var reminderDays = settings.ReminderDaysBefore
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var d) ? d : -1)
            .Where(d => d > 0).ToList();
        if (reminderDays.Count == 0) return new JobResult("bill-reminders", 0, "No reminder days configured.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTime.UtcNow;
        var bills = await _db.Bills.Include(b => b.RaisedBy)
            .Where(b => b.Status != BillStatus.Paid && b.Status != BillStatus.Rejected && b.DueDate != null)
            .AsNoTracking().ToListAsync(ct);

        var reminded = 0;
        foreach (var bill in bills)
        {
            var daysUntilDue = (bill.DueDate!.Value.Date - today.ToDateTime(TimeOnly.MinValue)).Days;
            if (!reminderDays.Contains(daysUntilDue)) continue;

            // Avoid duplicate reminders: check if LastReminderAt is within the last 20 hours
            if (bill.LastReminderAt.HasValue && (now - bill.LastReminderAt.Value).TotalHours < 20) continue;

            // Update LastReminderAt on the tracked entity
            var tracked = await _db.Bills.FindAsync(new object[] { bill.Id }, ct);
            if (tracked is not null)
            {
                tracked.LastReminderAt = now;
                await _db.SaveChangesAsync(ct);
            }

            var recipients = settings.ReminderRecipients
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var emails = recipients.Select(x => x.ToLowerInvariant()).Distinct().ToList();
            var userIds = await _db.Users.Where(u => emails.Contains(u.Email)).Select(u => u.Id).ToListAsync(ct);

            var title = $"Bill due in {daysUntilDue} day(s): {bill.Title}";
            var message = $"Bill '{bill.Title}' from {bill.Vendor} for ₹{bill.Amount:N0} is due on {bill.DueDate:dd MMM yyyy}.";
            if (userIds.Count > 0)
                await _notify.NotifyManyAsync(userIds, NotificationType.General, title, message, "/bills", ct);

            if (_integration.OutlookNotificationsLive && _azure.IsConfigured)
            {
                var html = $"<h3>{title}</h3><p>{System.Net.WebUtility.HtmlEncode(message)}</p>";
                foreach (var email in emails)
                    try { await _graph.SendMailAsync(email, title, html, ct); } catch { }
            }

            if (!string.IsNullOrWhiteSpace(settings.TeamsWebhookUrl))
                try { await _teams.SendAsync(settings.TeamsWebhookUrl!, title, message, ct); } catch { }

            reminded++;
        }
        return new JobResult("bill-reminders", reminded, $"Sent {reminded} bill reminder(s).");
    }

    // ---- Bill summaries ----
    public Task<JobResult> RunDailyBillSummaryAsync(CancellationToken ct = default)
        => BillSummaryAsync("Daily", DateTime.UtcNow.Date, s => s.DailySummaryRecipients, ct);

    public Task<JobResult> RunWeeklyBillSummaryAsync(CancellationToken ct = default)
        => BillSummaryAsync("Weekly", DateTime.UtcNow.Date.AddDays(-7), s => s.WeeklySummaryRecipients, ct);

    private async Task<JobResult> BillSummaryAsync(string label, DateTime from, Func<Domain.Entities.BillSetting, string> recipients, CancellationToken ct)
    {
        var settings = await _db.BillSettings.FirstOrDefaultAsync(ct);
        if (settings is null) return new JobResult($"{label.ToLower()}-bill-summary", 0, "No bill settings configured.");

        var bills = await _db.Bills.Include(b => b.RaisedBy)
            .Where(b => b.CreatedAt >= from).AsNoTracking().ToListAsync(ct);

        var total = bills.Sum(b => b.Amount);
        var pendingApproval = bills.Count(b => b.Status == BillStatus.Submitted && b.ApprovalRequired);
        var paid = bills.Where(b => b.Status == BillStatus.Paid).Sum(b => b.Amount);
        var overdue = bills.Count(b => b.Status == BillStatus.Overdue);

        var title = $"{label} Bill Summary — {DateTime.UtcNow:dd MMM yyyy}";
        var lines = bills.OrderByDescending(b => b.CreatedAt).Take(15)
            .Select(b => $"• {b.Title} — ₹{b.Amount.ToString("N0", CultureInfo.InvariantCulture)} [{b.Status}]");
        var body = $"{bills.Count} bill(s), total ₹{total.ToString("N0", CultureInfo.InvariantCulture)}. " +
                   $"Paid ₹{paid.ToString("N0", CultureInfo.InvariantCulture)}, {pendingApproval} awaiting approval, {overdue} overdue.\n\n" +
                   string.Join("\n", lines);
        var message = $"{bills.Count} bill(s), ₹{total.ToString("N0", CultureInfo.InvariantCulture)} total; {pendingApproval} awaiting approval, {overdue} overdue.";

        var emails = (recipients(settings) ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant()).Distinct().ToList();

        var users = await _db.Users.Where(u => emails.Contains(u.Email)).Select(u => u.Id).ToListAsync(ct);
        if (users.Count > 0)
            await _notify.NotifyManyAsync(users, NotificationType.ReportGenerated, title, message, "/bills", ct);

        if (_integration.OutlookNotificationsLive && _azure.IsConfigured)
        {
            var html = $"<h3>{title}</h3><p>{System.Net.WebUtility.HtmlEncode(message)}</p><pre style='font-family:inherit'>{System.Net.WebUtility.HtmlEncode(string.Join("\n", lines))}</pre>";
            foreach (var email in emails)
                try { await _graph.SendMailAsync(email, title, html, ct); } catch { }
        }

        if (!string.IsNullOrWhiteSpace(settings.TeamsWebhookUrl))
            try { await _teams.SendAsync(settings.TeamsWebhookUrl!, title, body, ct); } catch { }

        return new JobResult($"{label.ToLower()}-bill-summary", emails.Count,
            $"{label} summary sent to {emails.Count} recipient(s): {bills.Count} bills, ₹{total.ToString("N0", CultureInfo.InvariantCulture)}.");
    }
}
