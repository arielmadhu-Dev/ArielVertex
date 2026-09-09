using System.ComponentModel.DataAnnotations;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Application.Contracts;

public record AssetDto(
    int Id, string Name, string Description, AssetCategory Category, string CategoryLabel,
    string? SerialNumber, DateTime? PurchaseDate, decimal? PurchasePrice,
    AssetCondition Condition, string ConditionLabel, AssetStatus Status, string StatusLabel,
    int? AssignedToId, string? AssignedToName, DateTime? AssignedAt, DateTime? ReturnedAt,
    DateTime CreatedAt);

public record CreateAssetRequest(
    [Required, MaxLength(200)] string Name,
    string? Description,
    AssetCategory Category,
    string? SerialNumber,
    DateTime? PurchaseDate,
    decimal? PurchasePrice,
    AssetCondition Condition);

public record UpdateAssetRequest(
    string? Name, string? Description, AssetCategory? Category, string? SerialNumber,
    DateTime? PurchaseDate, decimal? PurchasePrice, AssetCondition? Condition,
    AssetStatus? Status, int? AssignedToId);

public record AssetRequestDto(
    int Id, string Subject, string Description, AssetRequestType RequestType, string RequestTypeLabel,
    int? AssetId, string? AssetName, int Quantity, decimal? EstimatedCost, string? Vendor,
    AssetRequestStatus Status, string StatusLabel,
    int RaisedById, string RaisedByName, int? TargetUserId, string? TargetUserName,
    string? ApproverName, DateTime? DecidedAt,
    string? DecisionNote, DateTime CreatedAt);

public record CreateAssetRequestRequest(
    [Required, MaxLength(200)] string Subject,
    [Required, MaxLength(4000)] string Description,
    AssetRequestType RequestType,
    int? AssetId,
    int? TargetUserId,
    int Quantity,
    decimal? EstimatedCost,
    string? Vendor);

public record AssetRequestDecisionRequest([MaxLength(2000)] string? Note);
