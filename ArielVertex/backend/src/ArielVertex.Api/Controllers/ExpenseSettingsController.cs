using ArielVertex.Api.Common;
using ArielVertex.Api.Security;
using ArielVertex.Application.Abstractions;
using ArielVertex.Application.Contracts;
using ArielVertex.Application.Security;
using ArielVertex.Domain.Entities;
using ArielVertex.Domain.Enums;
using ArielVertex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArielVertex.Api.Controllers;

[Authorize]
[Route("api/v1/expense-settings")]
public class ExpenseSettingsController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;
    private readonly IAuditService _audit;
    public ExpenseSettingsController(AppDbContext db, ICurrentUser me, IAuditService audit) { _db = db; _me = me; _audit = audit; }

    private static string[] SplitArr(string csv) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        if (!_me.Has(Permissions.ExpensesConfigure) && !_me.Has(Permissions.ExpensesViewAll) && !_me.Has(Permissions.ExpensesManage))
            return Denied();
        var s = await _db.ExpenseSettings.FirstOrDefaultAsync() ?? new ExpenseSetting();
        return Ok(new ExpenseSettingsDto(s.ApprovalRequiredByDefault, SplitArr(s.ApproverRoles),
            s.DailySummaryRecipients, s.WeeklySummaryRecipients, s.TeamsWebhookUrl));
    }

    [HttpPut]
    [Capability(Permissions.ExpensesConfigure)]
    public async Task<IActionResult> Update([FromBody] UpdateExpenseSettingsRequest req)
    {
        var s = await _db.ExpenseSettings.FirstOrDefaultAsync();
        if (s is null) { s = new ExpenseSetting(); _db.ExpenseSettings.Add(s); }

        s.ApprovalRequiredByDefault = req.ApprovalRequiredByDefault;
        if (req.ApproverRoles is not null)
            s.ApproverRoles = string.Join(",", req.ApproverRoles.Where(r => Enum.TryParse<PortalRole>(r, out _)));
        s.DailySummaryRecipients = req.DailySummaryRecipients?.Trim() ?? "";
        s.WeeklySummaryRecipients = req.WeeklySummaryRecipients?.Trim() ?? "";
        s.TeamsWebhookUrl = string.IsNullOrWhiteSpace(req.TeamsWebhookUrl) ? null : req.TeamsWebhookUrl.Trim();
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(AuditAction.ExpenseSettingsChanged, "ExpenseSetting", s.Id, "Expense module settings updated.");
        return Ok(new { ok = true });
    }
}
