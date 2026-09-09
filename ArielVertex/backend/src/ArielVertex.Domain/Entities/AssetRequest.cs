using ArielVertex.Domain.Common;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Domain.Entities;

/// <summary>Request for a new asset, repair, or replacement — follows an approval workflow.</summary>
public class AssetRequest : BaseEntity
{
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AssetRequestType RequestType { get; set; }
    public int? AssetId { get; set; }
    public Asset? Asset { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal? EstimatedCost { get; set; }
    public string? Vendor { get; set; }
    public AssetRequestStatus Status { get; set; } = AssetRequestStatus.Pending;

    public int RaisedById { get; set; }
    public User? RaisedBy { get; set; }

    public int? TargetUserId { get; set; }
    public User? TargetUser { get; set; }

    public int? ApproverId { get; set; }
    public User? Approver { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionNote { get; set; }
}
