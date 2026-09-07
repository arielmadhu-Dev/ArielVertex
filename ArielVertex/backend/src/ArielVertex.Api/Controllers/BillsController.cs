using System.ComponentModel.DataAnnotations;
using ArielVertex.Application.Abstractions;
using ArielVertex.Application.Contracts;
using ArielVertex.Application.Security;
using ArielVertex.Api.Common;
using ArielVertex.Api.Security;
using ArielVertex.Domain.Entities;
using ArielVertex.Domain.Enums;
using ArielVertex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArielVertex.Api.Controllers;

[ApiController]
[Route("api/v1/bills")]
[Authorize]
[RequireFeature("bills")]
public class BillsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;
    private readonly IFileStorage _files;
    private readonly IAuditService _audit;
    private readonly INotificationService _notify;
    private readonly IBillExtractor _extractor;

    private bool CanApprove => _me.Has(Permissions.BillsApprove);
    private bool CanViewAll => _me.Has(Permissions.BillsViewAll) || CanApprove;
    private bool CanManage => _me.Has(Permissions.BillsManage);

    public BillsController(AppDbContext db, ICurrentUser me, IFileStorage files,
        IAuditService audit, INotificationService notify, IBillExtractor extractor)
    { _db = db; _me = me; _files = files; _audit = audit; _notify = notify; _extractor = extractor; }

    // ── List ────────────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct)
    {
        var q = _db.Bills
            .Include(b => b.RaisedBy).Include(b => b.Approver)
            .AsNoTracking();

        if (!CanViewAll)
            q = q.Where(b => b.RaisedById == _me.Id);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<BillStatus>(status, true, out var s))
            q = q.Where(b => b.Status == s);

        // Auto-flag overdue bills
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayDt = today.ToDateTime(TimeOnly.MinValue);
        var overdueBills = await q.Where(b => b.Status != BillStatus.Paid && b.Status != BillStatus.Rejected
            && b.DueDate != null && b.DueDate < todayDt).ToListAsync(ct);
        foreach (var b in overdueBills)
        {
            if (b.Status != BillStatus.Overdue)
            {
                b.Status = BillStatus.Overdue;
                await _audit.WriteAsync(AuditAction.BillOverdue, "Bill", b.Id, $"Bill '{b.Title}' is overdue.", ct);
            }
        }
        await _db.SaveChangesAsync(ct);

        var items = await q.OrderByDescending(b => b.CreatedAt).ToListAsync(ct);

        var dtos = items.Select(b =>
        {
            var daysUntilDue = b.DueDate.HasValue ? (b.DueDate.Value.Date - todayDt).Days : 999;
            return b.ToDto(CanApprove, CanManage, daysUntilDue);
        }).ToList();

        var summary = new BillSummaryDto(
            Total: dtos.Count,
            PendingApproval: dtos.Count(d => d.Status == BillStatus.Submitted),
            Paid: dtos.Count(d => d.Status == BillStatus.Paid),
            Overdue: dtos.Count(d => d.Status == BillStatus.Overdue),
            TotalAmount: dtos.Sum(d => d.Amount),
            PaidAmount: dtos.Where(d => d.Status == BillStatus.Paid).Sum(d => d.Amount));

        return Ok(new { items = dtos, summary });
    }

    // ── Create ──────────────────────────────────────────────────────────────────
    [HttpPost]
    [Capability(Permissions.BillsManage)]
    public async Task<IActionResult> Create([FromBody] CreateBillRequest req, CancellationToken ct)
    {
        var setting = await _db.BillSettings.FirstOrDefaultAsync(ct) ?? new BillSetting();
        var bill = new Bill
        {
            Title = req.Title.Trim(),
            Description = req.Description?.Trim() ?? "",
            Category = req.Category?.Trim() ?? "",
            Amount = req.Amount,
            Currency = "INR",
            Vendor = req.Vendor?.Trim() ?? "",
            BillDate = req.BillDate ?? DateTime.UtcNow,
            DueDate = req.DueDate,
            PaymentMethod = req.PaymentMethod?.Trim() ?? "",
            InvoiceNumber = req.InvoiceNumber?.Trim(),
            Status = BillStatus.Submitted,
            ApprovalRequired = req.ApprovalRequired || setting.ApprovalRequiredByDefault,
            RaisedById = _me.Id
        };
        _db.Bills.Add(bill);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(AuditAction.BillCreated, "Bill", bill.Id, $"Bill '{bill.Title}' created.", ct);

        if (bill.ApprovalRequired)
            await NotifyApproversAsync(bill, setting, ct);

        return Ok(new { bill.Id });
    }

    // ── AI Extract ──────────────────────────────────────────────────────────────
    [HttpPost("extract")]
    [Capability(Permissions.BillsManage)]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> Extract([FromForm] IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".pdf" or ".png" or ".jpg" or ".jpeg" or ".gif"))
            return BadRequest(new { error = "Unsupported file type. Please upload a PDF or image (PNG, JPG, GIF)." });

        if (!_extractor.IsConfigured)
            return StatusCode(503, new { error = "AI service not configured. Please fill the form manually." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var data = await _extractor.ExtractAsync(ms.ToArray(), file.FileName, file.ContentType, ct);

        if (data is null)
            return BadRequest(new { error = "Could not extract data from the file. Please fill the form manually." });

        return Ok(data);
    }

    // ── Upload Invoice ──────────────────────────────────────────────────────────
    [HttpPost("{id:int}/invoice")]
    [Capability(Permissions.BillsManage)]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> UploadInvoice(int id, [FromForm] IFormFile? file, CancellationToken ct)
    {
        var bill = await _db.Bills.FindAsync(new object[] { id }, ct);
        if (bill is null) return NotFound();
        if (bill.RaisedById != _me.Id && !CanViewAll) return Forbid();
        if (file is null || file.Length == 0) return BadRequest();

        var stored = await _files.SaveAsync(file.OpenReadStream(), file.FileName, file.ContentType, ct);
        bill.InvoiceStoragePath = stored.StorageKey;
        bill.InvoiceFileName = stored.OriginalName;
        bill.InvoiceContentType = stored.ContentType;
        bill.InvoiceSizeBytes = stored.SizeBytes;
        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    // ── Download Invoice ────────────────────────────────────────────────────────
    [HttpGet("{id:int}/invoice/download")]
    public async Task<IActionResult> DownloadInvoice(int id, CancellationToken ct)
    {
        var bill = await _db.Bills.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);
        if (bill is null) return NotFound();
        if (bill.RaisedById != _me.Id && !CanViewAll) return Forbid();
        if (string.IsNullOrEmpty(bill.InvoiceStoragePath)) return NotFound();

        var result = await _files.OpenAsync(bill.InvoiceStoragePath, bill.InvoiceFileName ?? "file", ct);
        if (result is null) return NotFound();
        var (stream, contentType, _) = result.Value;
        return File(stream, contentType, bill.InvoiceFileName);
    }

    // ── Approve ─────────────────────────────────────────────────────────────────
    [HttpPost("{id:int}/approve")]
    [Capability(Permissions.BillsApprove)]
    public async Task<IActionResult> Approve(int id, [FromBody] BillDecisionRequest req, CancellationToken ct)
    {
        var bill = await _db.Bills.Include(b => b.RaisedBy).FirstOrDefaultAsync(b => b.Id == id, ct);
        if (bill is null) return NotFound();
        if (bill.Status != BillStatus.Submitted) return BadRequest(new { error = "Only submitted bills can be approved." });

        bill.Status = BillStatus.Approved;
        bill.ApproverId = _me.Id;
        bill.DecidedAt = DateTime.UtcNow;
        bill.DecisionNote = req.Note?.Trim();
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(AuditAction.BillApproved, "Bill", bill.Id, $"Bill '{bill.Title}' approved.", ct);

        if (bill.RaisedBy is not null)
            await _notify.NotifyAsync(bill.RaisedById, NotificationType.General,
                "Bill approved", $"Your bill '{bill.Title}' has been approved.", "/bills", ct);

        return Ok();
    }

    // ── Reject ──────────────────────────────────────────────────────────────────
    [HttpPost("{id:int}/reject")]
    [Capability(Permissions.BillsApprove)]
    public async Task<IActionResult> Reject(int id, [FromBody] BillDecisionRequest req, CancellationToken ct)
    {
        var bill = await _db.Bills.Include(b => b.RaisedBy).FirstOrDefaultAsync(b => b.Id == id, ct);
        if (bill is null) return NotFound();
        if (bill.Status != BillStatus.Submitted) return BadRequest(new { error = "Only submitted bills can be rejected." });

        bill.Status = BillStatus.Rejected;
        bill.ApproverId = _me.Id;
        bill.DecidedAt = DateTime.UtcNow;
        bill.DecisionNote = req.Note?.Trim();
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(AuditAction.BillRejected, "Bill", bill.Id, $"Bill '{bill.Title}' rejected.", ct);

        if (bill.RaisedBy is not null)
            await _notify.NotifyAsync(bill.RaisedById, NotificationType.General,
                "Bill rejected", $"Your bill '{bill.Title}' has been rejected. {(string.IsNullOrEmpty(req.Note) ? "" : $"Note: {req.Note}")}", "/bills", ct);

        return Ok();
    }

    // ── Pay ─────────────────────────────────────────────────────────────────────
    [HttpPost("{id:int}/pay")]
    [Capability(Permissions.BillsManage)]
    public async Task<IActionResult> Pay(int id, CancellationToken ct)
    {
        var bill = await _db.Bills.FindAsync(new object[] { id }, ct);
        if (bill is null) return NotFound();
        if (bill.RaisedById != _me.Id && !CanViewAll) return Forbid();
        if (bill.Status == BillStatus.Paid) return BadRequest(new { error = "Bill is already paid." });
        if (bill.Status == BillStatus.Rejected) return BadRequest(new { error = "Cannot pay a rejected bill." });
        if (bill.ApprovalRequired && bill.Status != BillStatus.Approved)
            return BadRequest(new { error = "Bill must be approved before marking as paid." });

        bill.Status = BillStatus.Paid;
        bill.PaidAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(AuditAction.BillPaid, "Bill", bill.Id, $"Bill '{bill.Title}' marked as paid.", ct);
        return Ok();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────
    private async Task NotifyApproversAsync(Bill bill, BillSetting setting, CancellationToken ct)
    {
        var roles = setting.ApproverRoles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(r => Enum.TryParse<PortalRole>(r, true, out var pr) ? pr : (PortalRole?)null)
            .Where(r => r.HasValue).Select(r => r!.Value).ToList();
        var approverIds = await _db.Users.Where(u => roles.Contains(u.Role) && u.Id != _me.Id)
            .Select(u => u.Id).ToListAsync(ct);
        if (approverIds.Count > 0)
            await _notify.NotifyManyAsync(approverIds, NotificationType.General,
                "Bill awaiting approval", $"A new bill '{bill.Title}' requires your review.", "/bills", ct);
    }
}

internal static class BillMapper
{
    public static BillDto ToDto(this Bill b, bool canApprove, bool canManage, int daysUntilDue) =>
        new(b.Id, b.Title, b.Description, b.Category, b.Amount, b.Currency, b.Vendor,
            b.BillDate, b.DueDate, b.PaymentMethod, b.InvoiceNumber, b.InvoiceStoragePath != null, b.InvoiceFileName,
            b.Status, Labels.BillStatus(b.Status), b.ApprovalRequired,
            b.RaisedById, b.RaisedBy?.Name ?? "", b.Approver?.Name, b.DecidedAt, b.DecisionNote,
            b.PaidAt, b.CreatedAt, canApprove, canManage, daysUntilDue);
}
