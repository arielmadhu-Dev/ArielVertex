using ArielVertex.Domain.Common;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Domain.Entities;

/// <summary>
/// Security and business audit trail (spec section 9). Immutable once written; visible to
/// Super Admin. Never stores secrets or tokens.
/// </summary>
public class AuditLog : BaseEntity
{
    public int? ActorId { get; set; }
    public User? Actor { get; set; }
    public string ActorName { get; set; } = string.Empty;   // denormalized for durability

    public AuditAction Action { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int? EntityId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? CorrelationId { get; set; }
}
