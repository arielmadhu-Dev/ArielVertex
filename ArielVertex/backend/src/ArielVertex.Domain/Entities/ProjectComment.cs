using ArielVertex.Domain.Common;

namespace ArielVertex.Domain.Entities;

/// <summary>
/// Collaboration note on a project (spec 6.3 "business comments"). Visible to every project member
/// — the channel where business stakeholders and the delivery team exchange context.
/// </summary>
public class ProjectComment : BaseEntity
{
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public int AuthorId { get; set; }
    public User? Author { get; set; }

    public string Message { get; set; } = string.Empty;

    /// <summary>Optional hours the business stakeholder is logging against the project alongside the comment.</summary>
    public decimal Hours { get; set; }
}
