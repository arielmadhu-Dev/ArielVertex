namespace ArielVertex.Domain.Common;

/// <summary>
/// Base for all persisted aggregates. Timestamps are UTC and maintained by the
/// infrastructure layer (SaveChanges interceptor) so no service has to remember.
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
