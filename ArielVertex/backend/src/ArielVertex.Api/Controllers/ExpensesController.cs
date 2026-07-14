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
[RequireFeature("expenses")]
[Route("api/v1/expenses")]
public class ExpensesController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;
    private readonly IFileStorage _files;
    private readonly IAuditService _audit;
    private readonly INotificationService _notify;

    public ExpensesController(AppDbContext db, ICurrentUser me, IFileStorage files, IAuditService audit, INotificationService notify)
    { _db = db; _me = me; _files = files; _audit = audit; _notify = notify; }

    private bool CanApprove => _me.Has(Permissions.ExpensesApprove);
    private bool CanViewAll => _me.Has(Permissions.ExpensesViewAll) || CanApprove;
    private bool CanManage => _me.Has(Permissions.ExpensesManage);

    /// <summary>Expense list. Front desk sees the ones they raised; approvers/viewers see all.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status)
    {
        if (!CanManage && !CanViewAll) return Denied();
        var q = _db.Expenses.Include(e => e.RaisedBy).Include(e => e.Approver).AsNoTracking().AsQueryable();
        if (!CanViewAll) q = q.Where(e => e.RaisedById == _me.Id);
        if (Enum.TryParse<ExpenseStatus>(status, true, out var st)) q = q.Where(e => e.Status == st);

        var list = await q.OrderByDescending(e => e.CreatedAt).ToListAsync();
        var canManageAny = CanManage;
        var summary = new ExpenseSummaryDto(
            list.Count,
            list.Count(e => e.Status == ExpenseStatus.PaymentRequested && e.ApprovalRequired),
            list.Count(e => e.Status == ExpenseStatus.Paid),
            list.Sum(e => e.Amount),
            list.Where(e => e.Status == ExpenseStatus.Paid).Sum(e => e.Amount));
        return Ok(new { items = list.Select(e => e.ToDto(CanApprove && e.Status == ExpenseStatus.PaymentRequested, canManageAny && e.RaisedById == _me.Id)), summary });
    }

    [HttpPost]
    [Capability(Permissions.ExpensesManage)]
    public async Task<IActionResult> Create([FromBody] CreateExpenseRequest req)
    {
        var settings = await _db.ExpenseSettings.FirstOrDefaultAsync();
        var needsApproval = req.ApprovalRequired || (settings?.ApprovalRequiredByDefault ?? false && req.ApprovalRequired);

        var e = new Expense
        {
            Title = req.Title.Trim(), Description = req.Description?.Trim() ?? "", Category = req.Category,
            Amount = req.Amount, Vendor = req.Vendor?.Trim() ?? "", ExpenseDate = req.ExpenseDate.ToUniversalTime(),
            PaymentMethod = req.PaymentMethod?.Trim() ?? "", InvoiceNumber = req.InvoiceNumber?.Trim(),
            ApprovalRequired = req.ApprovalRequired, Status = ExpenseStatus.PaymentRequested, RaisedById = _me.Id
        };
        _db.Expenses.Add(e);
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(AuditAction.ExpensePaymentRequested, "Expense", e.Id, $"Expense '{e.Title}' (₹{e.Amount}) raised.");

        if (req.ApprovalRequired) await NotifyApproversAsync(e);
        return Ok(new { e.Id });
    }

    [HttpPost("{id:int}/invoice")]
    [Capability(Permissions.ExpensesManage)]
    [RequestSizeLimit(30_000_000)]
    public async Task<IActionResult> UploadInvoice(int id, [FromForm] IFormFile? file)
    {
        var e = await _db.Expenses.FindAsync(id);
        if (e is null) return Missing();
        if (e.RaisedById != _me.Id && !CanViewAll) return Denied();
        if (file is null || file.Length == 0) return BadInput("Choose an invoice file.");
        try
        {
            await using var s = file.OpenReadStream();
            var stored = await _files.SaveAsync(s, file.FileName, file.ContentType);
            e.InvoiceFileName = stored.OriginalName; e.InvoiceContentType = stored.ContentType;
            e.InvoiceSizeBytes = stored.SizeBytes; e.InvoiceStoragePath = stored.StorageKey;
        }
        catch (FileValidationException ex) { return BadInput(ex.Message); }
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpGet("{id:int}/invoice/download")]
    public async Task<IActionResult> DownloadInvoice(int id)
    {
        var e = await _db.Expenses.FindAsync(id);
        if (e is null || e.InvoiceStoragePath is null) return Missing("No invoice on file.");
        if (e.RaisedById != _me.Id && !CanViewAll) return Denied();
        var opened = await _files.OpenAsync(e.InvoiceStoragePath, e.InvoiceFileName ?? "invoice");
        if (opened is null) return Missing("The stored file is no longer available.");
        var (stream, contentType, fileName) = opened.Value;
        return File(stream, contentType, fileName);
    }

    [HttpPost("{id:int}/approve")]
    [Capability(Permissions.ExpensesApprove)]
    public async Task<IActionResult> Approve(int id, [FromBody] ExpenseDecisionRequest req)
    {
        var e = await _db.Expenses.FindAsync(id);
        if (e is null) return Missing();
        if (e.Status != ExpenseStatus.PaymentRequested) return BadInput("Only a pending payment request can be approved.");
        e.Status = ExpenseStatus.Approved; e.ApproverId = _me.Id; e.DecidedAt = DateTime.UtcNow; e.DecisionNote = req.Note?.Trim();
        await _db.SaveChangesAsync();
        await _notify.NotifyAsync(e.RaisedById, NotificationType.General, "Expense approved", $"'{e.Title}' was approved — you can mark it paid.", "/expenses");
        await _audit.WriteAsync(AuditAction.ExpenseApproved, "Expense", e.Id, $"Expense '{e.Title}' approved.");
        return Ok(new { ok = true });
    }

    [HttpPost("{id:int}/reject")]
    [Capability(Permissions.ExpensesApprove)]
    public async Task<IActionResult> Reject(int id, [FromBody] ExpenseDecisionRequest req)
    {
        var e = await _db.Expenses.FindAsync(id);
        if (e is null) return Missing();
        if (e.Status != ExpenseStatus.PaymentRequested) return BadInput("Only a pending payment request can be rejected.");
        e.Status = ExpenseStatus.Rejected; e.ApproverId = _me.Id; e.DecidedAt = DateTime.UtcNow; e.DecisionNote = req.Note?.Trim();
        await _db.SaveChangesAsync();
        await _notify.NotifyAsync(e.RaisedById, NotificationType.General, "Expense rejected", $"'{e.Title}' was rejected. {req.Note}", "/expenses");
        await _audit.WriteAsync(AuditAction.ExpenseRejected, "Expense", e.Id, $"Expense '{e.Title}' rejected.");
        return Ok(new { ok = true });
    }

    /// <summary>Front desk marks paid — allowed once approved, or immediately if approval wasn't required.</summary>
    [HttpPost("{id:int}/pay")]
    [Capability(Permissions.ExpensesManage)]
    public async Task<IActionResult> Pay(int id)
    {
        var e = await _db.Expenses.FindAsync(id);
        if (e is null) return Missing();
        if (e.RaisedById != _me.Id && !CanViewAll) return Denied();
        if (e.ApprovalRequired && e.Status != ExpenseStatus.Approved)
            return BadInput("This expense needs approval before it can be marked paid.");
        if (e.Status == ExpenseStatus.Paid) return BadInput("Already paid.");
        if (e.Status == ExpenseStatus.Rejected) return BadInput("Rejected expenses cannot be paid.");
        e.Status = ExpenseStatus.Paid; e.PaidAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(AuditAction.ExpensePaid, "Expense", e.Id, $"Expense '{e.Title}' marked paid.");
        return Ok(new { ok = true });
    }

    private async Task NotifyApproversAsync(Expense e)
    {
        var settings = await _db.ExpenseSettings.FirstOrDefaultAsync();
        var roles = (settings?.ApproverRoles ?? "HrDirector,Accountant")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(r => Enum.TryParse<PortalRole>(r, out var pr) ? (PortalRole?)pr : null)
            .Where(r => r.HasValue).Select(r => r!.Value).ToList();
        var approverIds = await _db.Users.Where(u => roles.Contains(u.Role)).Select(u => u.Id).ToListAsync();
        if (approverIds.Count > 0)
            await _notify.NotifyManyAsync(approverIds, NotificationType.General, "Expense needs approval",
                $"'{e.Title}' (₹{e.Amount}) is awaiting your approval.", "/expenses");
    }
}
