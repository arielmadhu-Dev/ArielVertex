using System.ComponentModel.DataAnnotations;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Application.Contracts;

public record HelpdeskTicketDto(
    int Id, string Subject, string Description, HelpdeskTicketCategory Category,
    HelpdeskTicketPriority Priority, HelpdeskTicketStatus Status,
    int RaisedById, string RaisedByName, int? AssignedToId, string? AssignedToName,
    string? Resolution, DateTime? ResolvedAt, DateTime? ClosedAt, DateTime CreatedAt);

public record CreateHelpdeskTicketRequest(
    [Required, MaxLength(200)] string Subject,
    [Required, MaxLength(4000)] string Description,
    HelpdeskTicketCategory Category,
    HelpdeskTicketPriority Priority);

public record UpdateHelpdeskTicketRequest(
    HelpdeskTicketStatus? Status,
    HelpdeskTicketPriority? Priority,
    int? AssignedToId,
    [MaxLength(4000)] string? Resolution);

/// <summary>A ticket category and its designated helpdesk support person.</summary>
public record HelpdeskCategoryAssignmentDto(
    HelpdeskTicketCategory Category, string CategoryLabel, int? UserId, string? UserName);

/// <summary>HR Director request to designate the helpdesk support person for a ticket category.</summary>
public record SetHelpdeskCategoryRequest(
    HelpdeskTicketCategory Category,
    int? UserId);
