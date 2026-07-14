using ArielVertex.Api.Security;
using ArielVertex.Application.Abstractions;
using ArielVertex.Application.Security;
using ArielVertex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArielVertex.Api.Controllers;

/// <summary>Role-aware global search across people, projects and the performance modules.</summary>
[Authorize]
[Route("api/v1/search")]
public class SearchController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;
    public SearchController(AppDbContext db, ICurrentUser me) { _db = db; _me = me; }

    public record SearchResult(string Type, int Id, string Title, string Subtitle, string Link);

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q)
    {
        q = (q ?? "").Trim();
        if (q.Length < 2) return Ok(Array.Empty<SearchResult>());

        var results = new List<SearchResult>();

        // People (visible to anyone who can see the directory / all projects).
        if (_me.Has(Permissions.EmployeesManage) || _me.Has(Permissions.ProjectsViewAll) || _me.Has(Permissions.GoalsViewAll))
        {
            var users = await _db.Users.AsNoTracking()
                .Where(u => u.Name.Contains(q) || u.Email.Contains(q) || u.Designation.Contains(q))
                .OrderBy(u => u.Name).Take(6).ToListAsync();
            results.AddRange(users.Select(u => new SearchResult("Person", u.Id, u.Name, u.Designation, "/employees")));
        }

        // Projects the caller can see.
        var seesAllProjects = _me.Has(Permissions.ProjectsViewAll);
        var projQ = _db.Projects.AsNoTracking().Where(p => p.Name.Contains(q) || p.Code.Contains(q));
        if (!seesAllProjects)
        {
            var myProjectIds = await _db.ProjectMembers.Where(m => m.UserId == _me.Id && m.IsActive)
                .Select(m => m.ProjectId).ToListAsync();
            projQ = projQ.Where(p => myProjectIds.Contains(p.Id));
        }
        var projects = await projQ.OrderBy(p => p.Name).Take(6).ToListAsync();
        results.AddRange(projects.Select(p => new SearchResult("Project", p.Id, p.Name, p.Code, $"/projects/{p.Id}")));

        // Goals (scoped): everyone's if GoalsViewAll, else own + reports.
        var goalsAll = _me.Has(Permissions.GoalsViewAll);
        var goalQ = _db.Goals.Include(g => g.Employee).AsNoTracking().Where(g => g.Title.Contains(q) || g.Category.Contains(q));
        if (!goalsAll) goalQ = goalQ.Where(g => g.EmployeeId == _me.Id || g.Employee!.ManagerId == _me.Id);
        var goals = await goalQ.OrderByDescending(g => g.CreatedAt).Take(6).ToListAsync();
        results.AddRange(goals.Select(g => new SearchResult("Goal", g.Id, g.Title, g.Employee?.Name ?? "", "/goals")));

        // Promotions (scoped).
        var promoAll = _me.Has(Permissions.PromotionsManage) || _me.Has(Permissions.PromotionsApprove);
        var promoQ = _db.Promotions.Include(p => p.Employee).AsNoTracking()
            .Where(p => p.ProposedDesignation.Contains(q) || p.Employee!.Name.Contains(q));
        if (!promoAll) promoQ = promoQ.Where(p => p.EmployeeId == _me.Id || p.RecommendedById == _me.Id || p.Employee!.ManagerId == _me.Id);
        var promos = await promoQ.OrderByDescending(p => p.CreatedAt).Take(5).ToListAsync();
        results.AddRange(promos.Select(p => new SearchResult("Promotion", p.Id, p.ProposedDesignation, p.Employee?.Name ?? "", "/promotions")));

        // Training (scoped).
        var trainAll = _me.Has(Permissions.GoalsViewAll);
        var trainQ = _db.TrainingRecommendations.Include(t => t.Employee).AsNoTracking()
            .Where(t => t.RecommendedTraining.Contains(q) || t.SkillGap.Contains(q));
        if (!trainAll) trainQ = trainQ.Where(t => t.EmployeeId == _me.Id || t.Employee!.ManagerId == _me.Id);
        var trainings = await trainQ.OrderByDescending(t => t.CreatedAt).Take(5).ToListAsync();
        results.AddRange(trainings.Select(t => new SearchResult("Training", t.Id, t.RecommendedTraining, t.Employee?.Name ?? "", "/learning")));

        return Ok(results);
    }
}
