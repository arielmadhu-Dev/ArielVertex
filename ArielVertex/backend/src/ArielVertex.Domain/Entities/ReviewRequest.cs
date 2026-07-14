using ArielVertex.Domain.Common;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Domain.Entities;

/// <summary>
/// HR/management request for a project, code, or general review (spec 6.7). Distinct from
/// legacy appraisal cycles — this drives the schedule-in-one-click PM/PC workflow.
/// </summary>
public class ReviewRequest : BaseEntity
{
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public int SubjectUserId { get; set; }                 // employee being reviewed
    public User? SubjectUser { get; set; }

    public int RequestedById { get; set; }
    public User? RequestedBy { get; set; }

    public int? AssignedToId { get; set; }                 // PM/PC/Tech Lead who will run it
    public User? AssignedTo { get; set; }

    public ReviewType ReviewType { get; set; } = ReviewType.ProjectReview;
    public ReviewRequestStatus Status { get; set; } = ReviewRequestStatus.Open;
    public string Notes { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public DateTime? ClosedAt { get; set; }

    public ReviewMeeting? Meeting { get; set; }
    public CodeReview? CodeReview { get; set; }
    public ProjectReview? ProjectReview { get; set; }
}
