using ArielVertex.Api.Common;
using ArielVertex.Api.Security;
using ArielVertex.Application.Abstractions;
using ArielVertex.Application.Common;
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
[Route("api/v1/projects")]
public class ProjectsController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly IProjectAccessService _access;
    private readonly ICurrentUser _me;
    private readonly IAuditService _audit;
    private readonly IGraphMeetingService _graph;
    private readonly INotificationService _notify;
    private readonly IFileStorage _files;

    public ProjectsController(AppDbContext db, IProjectAccessService access, ICurrentUser me,
        IAuditService audit, IGraphMeetingService graph, INotificationService notify, IFileStorage files)
    { _db = db; _access = access; _me = me; _audit = audit; _graph = graph; _notify = notify; _files = files; }

    // ---------------- Projects ----------------
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PageQuery q, [FromQuery] string? status)
    {
        var visible = await _access.VisibleProjectIdsAsync();
        var query = _db.Projects.Include(p => p.Members).AsNoTracking().AsQueryable();
        if (visible is not null) query = query.Where(p => visible.Contains(p.Id));
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(p => p.Name.Contains(q.Search) || p.Code.Contains(q.Search) || p.ClientName.Contains(q.Search));
        if (Enum.TryParse<ProjectStatus>(status, true, out var st)) query = query.Where(p => p.Status == st);

        var total = await query.CountAsync();
        var page = await query.OrderByDescending(p => p.Priority).ThenBy(p => p.Name)
            .Skip(q.Skip).Take(q.SafeSize).ToListAsync();

        var items = page.Select(p =>
        {
            var myRole = p.Members.FirstOrDefault(m => m.UserId == _me.Id && m.IsActive)?.RoleOnProject.ToString();
            return p.ToListItem(p.Members.Count(m => m.IsActive), myRole);
        }).ToList();

        return Ok(new PagedResult<ProjectListItemDto>(items, total, q.SafePage, q.SafeSize));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        if (!await _access.CanViewAsync(id)) return Denied();
        var p = await _db.Projects.Include(x => x.Members).ThenInclude(m => m.User)
            .AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return Missing("Project not found.");
        var myRole = (await _access.RoleOnProjectAsync(id))?.ToString();
        return Ok(p.ToDetail(await _access.CanManageAsync(id), myRole));
    }

    [HttpPost]
    [Capability(Permissions.ProjectsCreate)]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest req)
    {
        if (await _db.Projects.AnyAsync(p => p.Code == req.Code))
            return Conflict409("A project with this code already exists.");

        var p = new Project
        {
            Code = req.Code.Trim(), Name = req.Name.Trim(), Description = req.Description?.Trim() ?? "",
            Status = req.Status, Priority = req.Priority, StartDate = req.StartDate.ToUniversalTime(),
            ExpectedEndDate = req.ExpectedEndDate?.ToUniversalTime(), ClientName = req.ClientName?.Trim() ?? "",
            BusinessOwner = req.BusinessOwner?.Trim() ?? "", Tags = req.Tags?.Trim() ?? "", Notes = req.Notes?.Trim() ?? ""
        };
        _db.Projects.Add(p);
        await _db.SaveChangesAsync();

        // Creator becomes PM so they retain manage access immediately (unless privileged).
        _db.ProjectMembers.Add(new ProjectMember
        { ProjectId = p.Id, UserId = _me.Id, RoleOnProject = ProjectRole.ProjectManager, AllocationPct = 0, StartDate = p.StartDate, IsActive = true });
        await _db.SaveChangesAsync();

        await _audit.WriteAsync(AuditAction.ProjectAssignment, "Project", p.Id, $"Project {p.Code} created.");
        return Ok(p.ToDetail(true, ProjectRole.ProjectManager.ToString()));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProjectRequest req)
    {
        if (!await _access.CanManageAsync(id)) return Denied("Only the PM/PC of this project can edit it.");
        var p = await _db.Projects.FindAsync(id);
        if (p is null) return Missing("Project not found.");

        p.Name = req.Name.Trim(); p.Description = req.Description?.Trim() ?? ""; p.Status = req.Status;
        p.Health = req.Health; p.Priority = req.Priority; p.StartDate = req.StartDate.ToUniversalTime();
        p.ExpectedEndDate = req.ExpectedEndDate?.ToUniversalTime(); p.ClientName = req.ClientName?.Trim() ?? "";
        p.BusinessOwner = req.BusinessOwner?.Trim() ?? ""; p.Tags = req.Tags?.Trim() ?? ""; p.Notes = req.Notes?.Trim() ?? "";
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    // ---------------- Members ----------------
    [HttpGet("{id:int}/members")]
    public async Task<IActionResult> Members(int id)
    {
        if (!await _access.CanViewAsync(id)) return Denied();
        var members = await _db.ProjectMembers.Include(m => m.User)
            .Where(m => m.ProjectId == id && m.IsActive).AsNoTracking().ToListAsync();
        return Ok(members.Select(m => m.ToDto()).OrderBy(m => m.RoleOnProject));
    }

    [HttpPost("{id:int}/members")]
    public async Task<IActionResult> AddMember(int id, [FromBody] AddMemberRequest req)
    {
        if (!await _access.CanManageAsync(id)) return Denied("Only the PM/PC can manage the team.");
        if (!await _db.Users.AnyAsync(u => u.Id == req.UserId)) return BadInput("Unknown employee.");

        // Preserve membership history on removal, but reactivate that row when the employee is
        // added again. The unique project/user index prevents creating a second membership row.
        var member = await _db.ProjectMembers.FirstOrDefaultAsync(m => m.ProjectId == id && m.UserId == req.UserId);
        if (member?.IsActive == true) return Conflict409("That person is already on the project.");
        if (member is null)
        {
            member = new ProjectMember { ProjectId = id, UserId = req.UserId };
            _db.ProjectMembers.Add(member);
        }
        member.RoleOnProject = req.RoleOnProject;
        member.AllocationPct = req.AllocationPct;
        member.StartDate = req.StartDate.ToUniversalTime();
        member.EndDate = req.EndDate?.ToUniversalTime();
        member.IsActive = true;
        await _db.SaveChangesAsync();
        await _notify.NotifyAsync(req.UserId, NotificationType.General, "Added to a project",
            "You have been assigned to a project. Open the workspace to see details.", $"/projects/{id}");
        await _audit.WriteAsync(AuditAction.ProjectAssignment, "Project", id, $"Member {req.UserId} added.");
        return Ok(new { ok = true });
    }

    [HttpDelete("{id:int}/members/{memberId:int}")]
    public async Task<IActionResult> RemoveMember(int id, int memberId)
    {
        if (!await _access.CanManageAsync(id)) return Denied();
        var m = await _db.ProjectMembers.FirstOrDefaultAsync(x => x.Id == memberId && x.ProjectId == id);
        if (m is null) return Missing();
        m.IsActive = false; m.EndDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(AuditAction.ProjectAssignment, "Project", id, $"Member {m.UserId} removed.");
        return NoContent();
    }

    // ---------------- Documents ----------------
    [HttpGet("{id:int}/documents")]
    public async Task<IActionResult> Documents(int id)
    {
        if (!await _access.CanViewAsync(id)) return Denied();
        var role = await _access.RoleOnProjectAsync(id);
        var isBusiness = role == ProjectRole.BusinessPerson && !_me.Has(Permissions.ProjectsViewAll);

        var docs = await _db.ProjectDocuments.Include(d => d.UploadedBy).Where(d => d.ProjectId == id)
            .AsNoTracking().OrderByDescending(d => d.CreatedAt).ToListAsync();
        // Business members only see business-visible / client-shareable docs.
        if (isBusiness) docs = docs.Where(d => d.Visibility != Visibility.Internal).ToList();

        return Ok(docs.Select(d => new
        {
            d.Id, d.Title, d.Description, d.Category, d.Visibility, d.IsVideoLink, d.VideoUrl,
            d.FileName, d.SizeBytes, UploadedBy = d.UploadedBy?.Name, d.CreatedAt
        }));
    }

    [HttpPost("{id:int}/documents")]
    [Capability(Permissions.DocumentsUpload)]
    public async Task<IActionResult> AddDocument(int id, [FromBody] AddDocumentRequest req)
    {
        if (!await _access.CanViewAsync(id)) return Denied();
        var doc = new ProjectDocument
        {
            ProjectId = id, Title = req.Title.Trim(), Description = req.Description?.Trim() ?? "",
            Category = req.Category, Visibility = req.Visibility, IsVideoLink = req.IsVideoLink,
            VideoUrl = req.IsVideoLink ? req.VideoUrl : null, FileName = req.IsVideoLink ? null : req.FileName,
            UploadedById = _me.Id
        };
        _db.ProjectDocuments.Add(doc);
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(AuditAction.DocumentUploaded, "ProjectDocument", doc.Id, $"Document '{doc.Title}' added to project {id}.");
        return Ok(new { doc.Id });
    }

    /// <summary>Upload a real file into private storage (spec 6.4). Type/size validated by IFileStorage.</summary>
    [HttpPost("{id:int}/documents/upload")]
    [Capability(Permissions.DocumentsUpload)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(30_000_000)]
    public async Task<IActionResult> UploadDocument(int id, [FromForm] UploadProjectDocumentRequest req)
    {
        if (!await _access.CanViewAsync(id)) return Denied();
        if (req.File is null || req.File.Length == 0) return BadInput("Please choose a file to upload.");
        if (string.IsNullOrWhiteSpace(req.Title)) return BadInput("A title is required.");

        StoredFile stored;
        try
        {
            await using var stream = req.File.OpenReadStream();
            stored = await _files.SaveAsync(stream, req.File.FileName, req.File.ContentType);
        }
        catch (FileValidationException ex) { return BadInput(ex.Message); }

        var doc = new ProjectDocument
        {
            ProjectId = id,
            Title = req.Title.Trim(),
            Description = req.Description?.Trim() ?? "",
            Category = req.Category,
            Visibility = req.Visibility,
            IsVideoLink = false,
            FileName = stored.OriginalName,
            ContentType = stored.ContentType,
            SizeBytes = stored.SizeBytes,
            StoragePath = stored.StorageKey,
            UploadedById = _me.Id
        };
        _db.ProjectDocuments.Add(doc);
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(AuditAction.DocumentUploaded, "ProjectDocument", doc.Id, $"File '{doc.Title}' ({doc.FileName}) uploaded to project {id}.");
        return Ok(new { doc.Id });
    }

    /// <summary>Authorized download — the only way to reach a stored file. StoragePath is never exposed.</summary>
    [HttpGet("{id:int}/documents/{docId:int}/download")]
    public async Task<IActionResult> DownloadDocument(int id, int docId)
    {
        if (!await _access.CanViewAsync(id)) return Denied();
        var doc = await _db.ProjectDocuments.FirstOrDefaultAsync(d => d.Id == docId && d.ProjectId == id);
        if (doc is null || doc.IsVideoLink || doc.StoragePath is null) return Missing("File not found.");

        // Business members can't download internal-only documents.
        var role = await _access.RoleOnProjectAsync(id);
        var isBusiness = role == ProjectRole.BusinessPerson && !_me.Has(Permissions.ProjectsViewAll);
        if (isBusiness && doc.Visibility == Visibility.Internal) return Denied("This document is internal only.");

        var opened = await _files.OpenAsync(doc.StoragePath, doc.FileName ?? "download");
        if (opened is null) return Missing("The stored file is no longer available.");
        var (stream, contentType, fileName) = opened.Value;
        return File(stream, contentType, fileName);
    }

    [HttpDelete("{id:int}/documents/{docId:int}")]
    [Capability(Permissions.DocumentsDelete)]
    public async Task<IActionResult> DeleteDocument(int id, int docId)
    {
        if (!await _access.CanManageAsync(id)) return Denied("Only the PM/PC can remove documents.");
        var doc = await _db.ProjectDocuments.FirstOrDefaultAsync(d => d.Id == docId && d.ProjectId == id);
        if (doc is null) return Missing();
        if (!doc.IsVideoLink && doc.StoragePath is not null) _files.Delete(doc.StoragePath);
        _db.ProjectDocuments.Remove(doc);
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(AuditAction.DocumentDeleted, "ProjectDocument", docId, $"Document '{doc.Title}' removed from project {id}.");
        return NoContent();
    }

    // ---------------- Calls ----------------
    [HttpGet("{id:int}/calls")]
    public async Task<IActionResult> Calls(int id)
    {
        if (!await _access.CanViewAsync(id)) return Denied();
        var calls = await _db.ProjectCalls.Where(c => c.ProjectId == id).AsNoTracking()
            .OrderByDescending(c => c.ScheduledAt).ToListAsync();
        return Ok(calls.Select(c => new
        {
            c.Id, c.Title, c.Agenda, c.Type, c.ScheduledAt, c.DurationMinutes, c.Attendees,
            c.TeamsJoinUrl, c.MeetingNotes, c.FollowUpActionItems
        }));
    }

    [HttpPost("{id:int}/calls")]
    [Capability(Permissions.CallsManage)]
    public async Task<IActionResult> ScheduleCall(int id, [FromBody] ScheduleCallRequest req)
    {
        if (!await _access.CanManageAsync(id)) return Denied("Only the PM/PC can schedule calls.");
        var attendees = Mappers.SplitAttendees(req.Attendees);
        (string eventId, string joinUrl) calendar;
        try
        {
            calendar = await _graph.CreateMeetingAsync(
                req.Title, req.Agenda ?? "", req.ScheduledAt.ToUniversalTime(), req.DurationMinutes, attendees);
        }
        catch (MeetingIntegrationException ex)
        {
            return Fail(StatusCodes.Status503ServiceUnavailable, "calendar_integration_unavailable", ex.Message);
        }
        var (eventId, joinUrl) = calendar;

        var call = new ProjectCall
        {
            ProjectId = id, Title = req.Title.Trim(), Agenda = req.Agenda?.Trim() ?? "", Type = req.Type,
            ScheduledAt = req.ScheduledAt.ToUniversalTime(), DurationMinutes = req.DurationMinutes,
            Attendees = req.Attendees ?? "", OutlookEventId = eventId, TeamsJoinUrl = joinUrl, ScheduledById = _me.Id
        };
        _db.ProjectCalls.Add(call);
        await _db.SaveChangesAsync();
        return Ok(new { call.Id, call.TeamsJoinUrl, graphLive = _graph.IsLive });
    }

    // ---------------- Business comments ----------------
    [HttpGet("{id:int}/comments")]
    public async Task<IActionResult> Comments(int id)
    {
        if (!await _access.CanViewAsync(id)) return Denied();
        var comments = await _db.ProjectComments.Include(c => c.Author).Where(c => c.ProjectId == id)
            .AsNoTracking().OrderBy(c => c.CreatedAt).ToListAsync();
        return Ok(comments.Select(c => new
        {
            c.Id, c.Message, AuthorId = c.AuthorId, AuthorName = c.Author?.Name,
            AvatarColor = c.Author?.AvatarColor ?? "#1E7FD4", Role = c.Author?.Role.ToString(), c.CreatedAt
        }));
    }

    [HttpPost("{id:int}/comments")]
    public async Task<IActionResult> AddComment(int id, [FromBody] AddCommentRequest req)
    {
        if (!await _access.CanViewAsync(id)) return Denied();
        if (string.IsNullOrWhiteSpace(req.Message)) return BadInput("Comment cannot be empty.");
        var c = new ProjectComment { ProjectId = id, AuthorId = _me.Id, Message = req.Message.Trim() };
        _db.ProjectComments.Add(c);
        await _db.SaveChangesAsync();
        return Ok(new { c.Id });
    }

    // ---------------- Status updates (project-scoped view) ----------------
    [HttpGet("{id:int}/status-updates")]
    public async Task<IActionResult> StatusUpdates(int id)
    {
        if (!await _access.CanViewAsync(id)) return Denied();
        var role = await _access.RoleOnProjectAsync(id);
        var canSeeInternal = _me.Has(Permissions.StatusViewAll) || await _access.CanManageAsync(id);
        var isBusiness = role == ProjectRole.BusinessPerson && !canSeeInternal;

        var updates = await _db.StatusUpdates.Include(s => s.User).Include(s => s.Project)
            .Where(s => s.ProjectId == id).AsNoTracking()
            .OrderByDescending(s => s.UpdateDate).ThenByDescending(s => s.CreatedAt).ToListAsync();

        if (isBusiness)
            return Ok(updates.Select(s => s.ToBusinessDto()));   // consolidated, no internal notes/blockers
        return Ok(updates.Select(s => s.ToDto(canSeeInternal)));

    }
}


public class UploadProjectDocumentRequest
{
    public IFormFile? File { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DocumentCategory Category { get; set; }
    public Visibility Visibility { get; set; }
}

