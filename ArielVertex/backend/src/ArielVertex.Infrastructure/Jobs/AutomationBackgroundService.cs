using ArielVertex.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArielVertex.Infrastructure.Jobs;

public class AutomationSettings
{
    public bool Enabled { get; set; } = true;
    /// <summary>How often the scheduler wakes to run due jobs.</summary>
    public int IntervalMinutes { get; set; } = 360; // 6h
    /// <summary>Delay before the first run after startup, so boot/seed settles.</summary>
    public int StartupDelaySeconds { get; set; } = 30;
}

/// <summary>
/// Timer-driven scheduler that runs the operational automations. Kept dependency-light (no
/// Hangfire/Quartz) — a hosted service with a periodic timer, resolving a fresh scope per cycle.
/// Jobs are idempotent, so a coarse interval is safe.
/// </summary>
public class AutomationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly AutomationSettings _settings;
    private readonly ILogger<AutomationBackgroundService> _log;

    public AutomationBackgroundService(IServiceScopeFactory scopes, IOptions<AutomationSettings> settings, ILogger<AutomationBackgroundService> log)
    { _scopes = scopes; _settings = settings.Value; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled) { _log.LogInformation("Automation disabled."); return; }
        try { await Task.Delay(TimeSpan.FromSeconds(_settings.StartupDelaySeconds), stoppingToken); } catch { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCycleAsync(stoppingToken);
            try { await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, _settings.IntervalMinutes)), stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    private DateOnly _lastDaily, _lastWeekly, _lastBillDaily, _lastBillWeekly;

    private async Task RunCycleAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var jobs = scope.ServiceProvider.GetRequiredService<IJobService>();
            var r1 = await jobs.RunStatusRemindersAsync(ct);
            var now = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(now);

            // Quarterly feedback in the last week of a quarter's end month (Mar/Jun/Sep/Dec).
            if (now.Month % 3 == 0 && now.Day >= 24) await jobs.RunQuarterlyFeedbackRequestsAsync(ct);
            // Monthly report generation in the first days of a month.
            if (now.Day <= 3) await jobs.RunMonthlyReportGenerationAsync(ct);

            // Every-evening expense summary (once per day, after 18:00 UTC).
            if (now.Hour >= 18 && today != _lastDaily) { await jobs.RunDailyExpenseSummaryAsync(ct); _lastDaily = today; }
            // Weekly expense summary on Fridays (once).
            if (now.DayOfWeek == DayOfWeek.Friday && now.Hour >= 18 && today != _lastWeekly) { await jobs.RunWeeklyExpenseSummaryAsync(ct); _lastWeekly = today; }

            // Bill due-date reminders (every cycle — check for 7d/3d/1d thresholds).
            if (await jobs.RunBillDueRemindersAsync(ct) is { } r3) { /* logged below */ }

            // Daily bill summary (every evening, once per day).
            if (now.Hour >= 18 && today != _lastBillDaily) { await jobs.RunDailyBillSummaryAsync(ct); _lastBillDaily = today; }
            // Weekly bill summary on Fridays.
            if (now.DayOfWeek == DayOfWeek.Friday && now.Hour >= 18 && today != _lastBillWeekly) { await jobs.RunWeeklyBillSummaryAsync(ct); _lastBillWeekly = today; }

            _log.LogInformation("Automation cycle complete. {Msg}", r1.Message);
        }
        catch (Exception ex) { _log.LogError(ex, "Automation cycle failed."); }
    }
}
