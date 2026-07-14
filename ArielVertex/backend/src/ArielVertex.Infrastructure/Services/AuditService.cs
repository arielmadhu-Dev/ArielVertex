using ArielVertex.Application.Abstractions;
using ArielVertex.Domain.Entities;
using ArielVertex.Domain.Enums;
using ArielVertex.Infrastructure.Persistence;

namespace ArielVertex.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;
    public AuditService(AppDbContext db, ICurrentUser me) { _db = db; _me = me; }

    public async Task WriteAsync(AuditAction action, string entityType, int? entityId, string summary, CancellationToken ct = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = _me.IsAuthenticated ? _me.Id : null,
            ActorName = _me.IsAuthenticated ? _me.Name : "system",
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Summary = summary,
            IpAddress = _me.IpAddress,
            CorrelationId = _me.CorrelationId
        });
        await _db.SaveChangesAsync(ct);
    }
}
