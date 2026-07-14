using ArielVertex.Api.Common;
using ArielVertex.Api.Security;
using ArielVertex.Application.Contracts;
using ArielVertex.Application.Security;
using ArielVertex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArielVertex.Api.Controllers;

[Authorize]
[Route("api/v1/resources")]
public class ResourcesController : ApiControllerBase
{
    private readonly AppDbContext _db;
    public ResourcesController(AppDbContext db) => _db = db;

    /// <summary>Allocation across projects: free / partially available / fully allocated / overloaded (spec 6.10).</summary>
    [HttpGet("occupancy")]
    [Capability(Permissions.ResourcesViewAll)]
    public async Task<IActionResult> Occupancy()
    {
        var members = await _db.ProjectMembers.Include(m => m.User).Include(m => m.Project)
            .Where(m => m.IsActive).AsNoTracking().ToListAsync();

        var rows = members.GroupBy(m => m.User!)
            .Select(g =>
            {
                var total = g.Sum(x => x.AllocationPct);
                var availability = total switch
                {
                    0 => "Free",
                    < 60 => "Partially Available",
                    <= 100 => "Fully Allocated",
                    _ => "Overloaded"
                };
                return new ResourceAllocationDto(
                    g.Key.Id, g.Key.Name, g.Key.Designation, g.Key.AvatarColor, total, availability,
                    g.Select(x => new AllocationLineDto(x.ProjectId, x.Project!.Name, x.RoleOnProject, x.AllocationPct)).ToList());
            })
            .OrderByDescending(r => r.TotalAllocationPct)
            .ToList();

        var buckets = new[]
        {
            new { Bucket = "Free", Count = rows.Count(r => r.Availability == "Free") },
            new { Bucket = "Partially Available", Count = rows.Count(r => r.Availability == "Partially Available") },
            new { Bucket = "Fully Allocated", Count = rows.Count(r => r.Availability == "Fully Allocated") },
            new { Bucket = "Overloaded", Count = rows.Count(r => r.Availability == "Overloaded") },
        };
        return Ok(new { rows, buckets });
    }
}
