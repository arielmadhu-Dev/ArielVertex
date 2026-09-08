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
using System.Text;

namespace ArielVertex.Api.Controllers;

[Authorize]
[RequireFeature("pettyCash")]
[Route("api/v1/petty-cash")]
public class PettyCashController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;
    private bool CanManage => _me.Has(Permissions.PettyCashManage);
    private bool CanViewAll => _me.Has(Permissions.PettyCashViewAll);

    public PettyCashController(AppDbContext db, ICurrentUser me)
    { _db = db; _me = me; }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var q = _db.PettyCashEntries.Include(e => e.CreatedBy).AsNoTracking().AsQueryable();
        if (!CanViewAll && !CanManage)
            q = q.Where(e => e.CreatedById == _me.Id);

        var list = await q.OrderBy(e => e.Date).ThenBy(e => e.Id).ToListAsync();
        return Ok(list.Select(e => e.ToDto(CanManage)));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePettyCashRequest req)
    {
        if (!CanManage) return Denied("Only HR Manager can create petty cash entries.");

        var previous = await _db.PettyCashEntries
            .OrderByDescending(e => e.Date).ThenByDescending(e => e.Id)
            .FirstOrDefaultAsync();

        var openingBalance = previous?.Balance ?? 0;
        var balance = openingBalance + req.Credit - req.Debit;

        var entry = new PettyCashEntry
        {
            Date = req.Date.ToUniversalTime(),
            Particulars = req.Particulars.Trim(),
            OpeningBalance = openingBalance,
            Credit = req.Credit,
            Debit = req.Debit,
            Balance = balance,
            Notes = req.Notes?.Trim(),
            CreatedById = _me.Id,
            Status = PettyCashEntryStatus.Approved
        };

        _db.PettyCashEntries.Add(entry);
        await _db.SaveChangesAsync();
        return Ok((await _db.PettyCashEntries.FindAsync(entry.Id)).ToDto(CanManage));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var e = await _db.PettyCashEntries.FindAsync(id);
        if (e is null) return Missing();
        if (!CanManage) return Denied();
        _db.PettyCashEntries.Remove(e);
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreatePettyCashRequest req)
    {
        var e = await _db.PettyCashEntries.FindAsync(id);
        if (e is null) return Missing();
        if (!CanManage) return Denied();

        var previous = await _db.PettyCashEntries
            .Where(x => x.Id != id)
            .OrderByDescending(x => x.Date).ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync();

        var openingBalance = previous?.Balance ?? 0;
        var balance = openingBalance + req.Credit - req.Debit;

        e.Date = req.Date.ToUniversalTime();
        e.Particulars = req.Particulars.Trim();
        e.OpeningBalance = openingBalance;
        e.Credit = req.Credit;
        e.Debit = req.Debit;
        e.Balance = balance;
        e.Notes = req.Notes?.Trim();
        await _db.SaveChangesAsync();

        var subsequent = await _db.PettyCashEntries
            .Where(x => x.Id != id && x.Date >= e.Date)
            .OrderBy(x => x.Date).ThenBy(x => x.Id)
            .ToListAsync();

        var lastBalance = balance;
        foreach (var s in subsequent)
        {
            s.OpeningBalance = lastBalance;
            s.Balance = lastBalance + s.Credit - s.Debit;
            lastBalance = s.Balance;
        }
        await _db.SaveChangesAsync();

        return Ok((await _db.PettyCashEntries.FindAsync(id)).ToDto(CanManage));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export()
    {
        var list = await _db.PettyCashEntries
            .Include(e => e.CreatedBy)
            .AsNoTracking()
            .OrderBy(e => e.Date).ThenBy(e => e.Id)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("SL No.,Date,Particulars,Opening Balance,Credit,Debit,Balance,Notes,Created By");

        foreach (var e in list)
        {
            var date = e.Date.ToLocalTime().ToString("yyyy-MM-dd");
            sb.AppendLine($"{e.Id},{date},{Csv(e.Particulars)},{e.OpeningBalance},{e.Credit},{e.Debit},{e.Balance},{Csv(e.Notes)},{Csv(e.CreatedBy?.Name)}");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", "petty-cash-ledger.csv");
    }

    private static string Csv(string? s) => string.IsNullOrWhiteSpace(s) ? "" : s.Contains(',') ? $"\"{s}\"" : s;
}
