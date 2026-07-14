using ArielVertex.Domain.Common;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Domain.Entities;

/// <summary>
/// Metadata for a project file or video link (spec 6.4). Files themselves live in private
/// storage; only authorized download endpoints resolve <see cref="StoragePath"/>.
/// </summary>
public class ProjectDocument : BaseEntity
{
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DocumentCategory Category { get; set; } = DocumentCategory.Other;
    public Visibility Visibility { get; set; } = Visibility.Internal;

    public bool IsVideoLink { get; set; }
    public string? VideoUrl { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public long SizeBytes { get; set; }
    public string? StoragePath { get; set; }               // never returned to clients directly

    public int UploadedById { get; set; }
    public User? UploadedBy { get; set; }
}
