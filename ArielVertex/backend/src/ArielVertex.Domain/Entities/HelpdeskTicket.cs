using ArielVertex.Domain.Common;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Domain.Entities;

/// <summary>Helpdesk support ticket raised by a user and resolved by System Admin.</summary>
public class HelpdeskTicket : BaseEntity
{
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public HelpdeskTicketCategory Category { get; set; } = HelpdeskTicketCategory.General;
    public HelpdeskTicketPriority Priority { get; set; } = HelpdeskTicketPriority.Medium;
    public HelpdeskTicketStatus Status { get; set; } = HelpdeskTicketStatus.Open;

    public int RaisedById { get; set; }
    public User? RaisedBy { get; set; }

    public int? AssignedToId { get; set; }
    public User? AssignedTo { get; set; }

    public string? Resolution { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}
