using ArielVertex.Domain.Common;

namespace ArielVertex.Domain.Entities;

public class ResourceRequestComment : BaseEntity
{
    public int ResourceRequestId { get; set; }
    public ResourceRequest? ResourceRequest { get; set; }

    public int AuthorId { get; set; }
    public User? Author { get; set; }

    public string Message { get; set; } = string.Empty;
}
