using ArielVertex.Domain.Common;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Domain.Entities;

/// <summary>Physical company asset (laptop, mouse, keyboard, monitor, PC, etc.).</summary>
public class Asset : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AssetCategory Category { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public decimal? PurchasePrice { get; set; }
    public AssetCondition Condition { get; set; } = AssetCondition.New;
    public AssetStatus Status { get; set; } = AssetStatus.Available;

    public int? AssignedToId { get; set; }
    public User? AssignedTo { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? ReturnedAt { get; set; }

    public int CreatedById { get; set; }
    public User? CreatedBy { get; set; }
}
