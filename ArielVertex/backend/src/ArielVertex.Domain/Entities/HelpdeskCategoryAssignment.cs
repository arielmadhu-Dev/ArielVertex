using ArielVertex.Domain.Common;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Domain.Entities;

/// <summary>
/// Designated helpdesk support person for a ticket category (chosen by the HR Director).
/// Tickets raised with a given category are auto-assigned to the mapped user.
/// </summary>
public class HelpdeskCategoryAssignment : BaseEntity
{
    public HelpdeskTicketCategory Category { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
}