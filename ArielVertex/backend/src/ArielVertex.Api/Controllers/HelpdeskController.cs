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
[Route("api/v1/helpdesk")]
public class HelpdeskController : ApiControllerBase
{
    private static readonly HelpdeskTicketCategory[] AllCategories = Enum.GetValues<HelpdeskTicketCategory>();

    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;
    private readonly INotificationService _notify;
    private bool CanManage => _me.Has(Permissions.HelpdeskManage);

    /// <summary>CEO, HR Director and System Admin see every ticket; everyone else only their own scope.</summary>
    private bool CanViewAll =>
        CanManage || _me.Role == PortalRole.CeoAdmin || _me.Role == PortalRole.HrDirector;

    public HelpdeskController(AppDbContext db, ICurrentUser me, INotificationService notify)
    { _db = db; _me = me; _notify = notify; }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var amISupport = await IsHelpdeskSupportAsync(_me.Id);

        var q = _db.HelpdeskTickets.Include(t => t.RaisedBy).Include(t => t.AssignedTo).AsNoTracking().AsQueryable();
        if (!CanViewAll)
            q = q.Where(t => t.RaisedById == _me.Id || (amISupport && t.AssignedToId == _me.Id));

        var list = await q.OrderByDescending(t => t.CreatedAt).ToListAsync();
        return Ok(list.Select(t => t.ToDto(CanManage)));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var amISupport = await IsHelpdeskSupportAsync(_me.Id);

        var t = await _db.HelpdeskTickets.Include(x => x.RaisedBy).Include(x => x.AssignedTo).FirstOrDefaultAsync(t => t.Id == id);
        if (t is null) return Missing();
        if (!CanViewAll && t.RaisedById != _me.Id && !(amISupport && t.AssignedToId == _me.Id)) return Denied();
        return Ok(t.ToDto(CanManage));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateHelpdeskTicketRequest req)
    {
        var ticket = new HelpdeskTicket
        {
            Subject = req.Subject.Trim(),
            Description = req.Description.Trim(),
            Category = req.Category,
            Priority = req.Priority,
            RaisedById = _me.Id,
            Status = HelpdeskTicketStatus.Open
        };

        // Auto-route to the support person designated for this ticket category (fallback: System Admin).
        ticket.AssignedToId = await ResolveAssigneeAsync(req.Category);

        _db.HelpdeskTickets.Add(ticket);
        await _db.SaveChangesAsync();

        if (ticket.AssignedToId.HasValue && ticket.AssignedToId != _me.Id)
        {
            await _notify.NotifyAsync(ticket.AssignedToId.Value, NotificationType.General,
                "Helpdesk ticket assigned",
                $"Ticket #{ticket.Id}: {ticket.Subject}",
                "/helpdesk");
        }

        if (ticket.RaisedById != _me.Id)
        {
            await _notify.NotifyAsync(ticket.RaisedById, NotificationType.General,
                "Helpdesk ticket created",
                $"Your ticket #{ticket.Id}: {ticket.Subject} has been raised and assigned.",
                "/helpdesk");
        }

        return Ok((await _db.HelpdeskTickets.FindAsync(ticket.Id)).ToDto(CanManage));
    }

    /// <summary>
    /// Picks the designated helpdesk support person for a ticket category.
    /// Falls back to the first active System Admin when no support person is configured for the category.
    /// Returns null only if neither a support person nor a System Admin exists.
    /// </summary>
    private async Task<int?> ResolveAssigneeAsync(HelpdeskTicketCategory category)
    {
        var support = await _db.HelpdeskCategoryAssignments.AsNoTracking()
            .Include(a => a.User)
            .Where(a => a.Category == category && a.User != null && a.User.Status == EmployeeStatus.Active)
            .Select(a => (int?)a.UserId)
            .FirstOrDefaultAsync();
        if (support.HasValue) return support.Value;

        var admin = await _db.Users.AsNoTracking()
            .Where(u => u.Role == PortalRole.SystemAdmin && u.Status == EmployeeStatus.Active)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync();
        return admin;
    }

    private async Task<bool> IsHelpdeskSupportAsync(int userId) =>
        await _db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.IsHelpdeskSupport).FirstOrDefaultAsync();

    [HttpGet("categories")]
    public async Task<IActionResult> CategoryAssignments()
    {
        if (_me.Role != PortalRole.HrDirector) return Denied("Only HR Director can manage helpdesk support personnel.");

        var assignments = await _db.HelpdeskCategoryAssignments.Include(a => a.User).AsNoTracking().ToListAsync();
        var byCategory = assignments.ToDictionary(a => a.Category);

        var result = AllCategories
            .Select(c => byCategory.TryGetValue(c, out var a) && a.User is not null
                ? new HelpdeskCategoryAssignmentDto(c, Labels.HelpdeskTicketCategoryLabel(c), a.User.Id, a.User.Name)
                : new HelpdeskCategoryAssignmentDto(c, Labels.HelpdeskTicketCategoryLabel(c), null, null))
            .ToList();

        return Ok(result);
    }

    [HttpPut("categories")]
    public async Task<IActionResult> SetCategory([FromBody] SetHelpdeskCategoryRequest req)
    {
        if (_me.Role != PortalRole.HrDirector) return Denied("Only HR Director can manage helpdesk support personnel.");
        if (!Enum.IsDefined(req.Category)) return BadRequest(new { message = "Invalid category." });

        var row = await _db.HelpdeskCategoryAssignments.FirstOrDefaultAsync(a => a.Category == req.Category);
        var previousUserId = row?.UserId;

        // Clearing the designation for a category.
        if (!req.UserId.HasValue)
        {
            if (row is not null)
            {
                _db.HelpdeskCategoryAssignments.Remove(row);
                await _db.SaveChangesAsync();
            }
            if (previousUserId.HasValue) await RefreshSupportFlagAsync(previousUserId.Value);
            return Ok();
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == req.UserId.Value);
        if (user is null || user.Status != EmployeeStatus.Active) return Missing("Selected support person not found.");

        if (row is null)
            _db.HelpdeskCategoryAssignments.Add(new HelpdeskCategoryAssignment { Category = req.Category, UserId = user.Id });
        else
            row.UserId = user.Id;
        await _db.SaveChangesAsync();

        await RefreshSupportFlagAsync(user.Id);
        if (previousUserId.HasValue && previousUserId.Value != user.Id) await RefreshSupportFlagAsync(previousUserId.Value);
        return Ok();
    }

    /// <summary>A user is "helpdesk support" when they are assigned at least one ticket category.</summary>
    private async Task RefreshSupportFlagAsync(int userId)
    {
        var count = await _db.HelpdeskCategoryAssignments.CountAsync(a => a.UserId == userId);
        await _db.Users.Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsHelpdeskSupport, count > 0));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateHelpdeskTicketRequest req)
    {
        var t = await _db.HelpdeskTickets.FindAsync(id);
        if (t is null) return Missing();

        var isAssignee = t.AssignedToId == _me.Id;
        if (!CanManage && !isAssignee) return Denied("Only System Admin or the assigned support person can update tickets.");

        var previousAssigneeId = t.AssignedToId;
        var previousStatus = t.Status;

        if (req.Status.HasValue) t.Status = req.Status.Value;
        if (req.Priority.HasValue)
        {
            if (!CanManage) return Denied("Only System Admin can change priority.");
            t.Priority = req.Priority.Value;
        }
        if (req.AssignedToId.HasValue)
        {
            if (!CanManage) return Denied("Only System Admin can reassign tickets.");
            t.AssignedToId = req.AssignedToId.Value;
        }
        if (req.Resolution is not null)
        {
            t.Resolution = req.Resolution.Trim();
            if (t.Status == HelpdeskTicketStatus.Open || t.Status == HelpdeskTicketStatus.InProgress)
            {
                t.Status = HelpdeskTicketStatus.Resolved;
                t.ResolvedAt = DateTime.UtcNow;
            }
        }
        await _db.SaveChangesAsync();

        if (req.AssignedToId.HasValue && req.AssignedToId.Value != previousAssigneeId && req.AssignedToId.Value != _me.Id)
        {
            await _notify.NotifyAsync(req.AssignedToId.Value, NotificationType.General,
                "Helpdesk ticket reassigned to you",
                $"Ticket #{t.Id}: {t.Subject}",
                "/helpdesk");
        }

        if (t.Status == HelpdeskTicketStatus.Resolved && previousStatus != HelpdeskTicketStatus.Resolved && t.RaisedById != _me.Id)
        {
            await _notify.NotifyAsync(t.RaisedById, NotificationType.General,
                "Helpdesk ticket resolved",
                $"Ticket #{t.Id}: {t.Subject} has been resolved.",
                "/helpdesk");
        }

        return Ok((await _db.HelpdeskTickets.FindAsync(id)).ToDto(CanManage));
    }
}