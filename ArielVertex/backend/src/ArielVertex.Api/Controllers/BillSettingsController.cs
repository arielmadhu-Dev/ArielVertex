using ArielVertex.Application.Abstractions;
using ArielVertex.Application.Contracts;
using ArielVertex.Application.Security;
using ArielVertex.Api.Security;
using ArielVertex.Domain.Entities;
using ArielVertex.Domain.Enums;
using ArielVertex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArielVertex.Api.Controllers;

[ApiController]
[Route("api/v1/bill-settings")]
[Authorize]
public class BillSettingsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentUser _me;

    public BillSettingsController(AppDbContext db, IAuditService audit, ICurrentUser me)
    { _db = db; _audit = audit; _me = me; }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (!_me.Has(Permissions.BillsConfigure) && !_me.Has(Permissions.BillsViewAll) && !_me.Has(Permissions.BillsManage))
            return Forbid();

        var s = await _db.BillSettings.FirstOrDefaultAsync(ct);
        if (s is null) return Ok(new BillSettingsDto(true, new[] { "HrDirector", "Accountant" }, "", "", "7,3,1", "", null));
        return Ok(new BillSettingsDto(
            s.ApprovalRequiredByDefault,
            s.ApproverRoles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            s.DailySummaryRecipients, s.WeeklySummaryRecipients,
            s.ReminderDaysBefore, s.ReminderRecipients,
            s.TeamsWebhookUrl));
    }

    [HttpPut]
    [Capability(Permissions.BillsConfigure)]
    public async Task<IActionResult> Update([FromBody] UpdateBillSettingsRequest req, CancellationToken ct)
    {
        var s = await _db.BillSettings.FirstOrDefaultAsync(ct) ?? new BillSetting();
        if (s.Id == 0) _db.BillSettings.Add(s);

        if (req.ApprovalRequiredByDefault.HasValue) s.ApprovalRequiredByDefault = req.ApprovalRequiredByDefault.Value;
        if (req.ApproverRoles is not null) s.ApproverRoles = string.Join(",", req.ApproverRoles);
        if (req.DailySummaryRecipients is not null) s.DailySummaryRecipients = req.DailySummaryRecipients;
        if (req.WeeklySummaryRecipients is not null) s.WeeklySummaryRecipients = req.WeeklySummaryRecipients;
        if (req.ReminderDaysBefore is not null) s.ReminderDaysBefore = req.ReminderDaysBefore;
        if (req.ReminderRecipients is not null) s.ReminderRecipients = req.ReminderRecipients;
        if (req.TeamsWebhookUrl is not null) s.TeamsWebhookUrl = req.TeamsWebhookUrl;

        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(AuditAction.BillSettingsChanged, "BillSetting", s.Id, "Bill settings updated.", ct);
        return Ok();
    }
}
