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
    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;
    private bool CanManage => _me.Has(Permissions.HelpdeskManage);

    public HelpdeskController(AppDbContext db, ICurrentUser me)
    { _db = db; _me = me; }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var q = _db.HelpdeskTickets.Include(t => t.RaisedBy).Include(t => t.AssignedTo).AsNoTracking().AsQueryable();
        if (!CanManage) q = q.Where(t => t.RaisedById == _me.Id);

        var list = await q.OrderByDescending(t => t.CreatedAt).ToListAsync();
        return Ok(list.Select(t => t.ToDto(CanManage)));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var t = await _db.HelpdeskTickets.Include(x => x.RaisedBy).Include(x => x.AssignedTo).FirstOrDefaultAsync(t => t.Id == id);
        if (t is null) return Missing();
        if (!CanManage && t.RaisedById != _me.Id) return Denied();
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

        _db.HelpdeskTickets.Add(ticket);
        await _db.SaveChangesAsync();
        return Ok((await _db.HelpdeskTickets.FindAsync(ticket.Id)).ToDto(CanManage));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateHelpdeskTicketRequest req)
    {
        if (!CanManage) return Denied("Only System Admin can update tickets.");

        var t = await _db.HelpdeskTickets.FindAsync(id);
        if (t is null) return Missing();

        if (req.Status.HasValue) t.Status = req.Status.Value;
        if (req.Priority.HasValue) t.Priority = req.Priority.Value;
        if (req.AssignedToId.HasValue) t.AssignedToId = req.AssignedToId.Value;
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
        return Ok((await _db.HelpdeskTickets.FindAsync(id)).ToDto(CanManage));
    }
}
