using ArielVertex.Domain.Common;

namespace ArielVertex.Domain.Entities;

public class Department : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public ICollection<User> Members { get; set; } = new List<User>();
}
