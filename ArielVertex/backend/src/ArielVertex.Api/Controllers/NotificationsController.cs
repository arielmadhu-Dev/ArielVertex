using ArielVertex.Api.Common;
using ArielVertex.Application.Abstractions;
using ArielVertex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArielVertex.Api.Controllers;

[Authorize]
[Route("api/v1/notifications")]
public class NotificationsController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;
    public NotificationsController(AppDbContext db, ICurrentUser me) { _db = db; _me = me; }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var items = await _db.Notifications.Where(n => n.RecipientId == _me.Id)
            .AsNoTracking().OrderByDescending(n => n.CreatedAt).Take(50).ToListAsync();
        return Ok(new { items = items.Select(n => n.ToDto()), unread = items.Count(n => !n.IsRead) });
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> Read(int id)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.RecipientId == _me.Id);
        if (n is null) return Missing();
        n.IsRead = true;
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll()
    {
        var unread = await _db.Notifications.Where(n => n.RecipientId == _me.Id && !n.IsRead).ToListAsync();
        unread.ForEach(n => n.IsRead = true);
        await _db.SaveChangesAsync();
        return Ok(new { ok = true, cleared = unread.Count });
    }
}
