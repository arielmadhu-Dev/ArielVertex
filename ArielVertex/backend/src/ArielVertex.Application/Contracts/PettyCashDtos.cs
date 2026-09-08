using System.ComponentModel.DataAnnotations;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Application.Contracts;

public record PettyCashDto(
    int Id, DateTime Date, string Particulars, decimal OpeningBalance,
    decimal Credit, decimal Debit, decimal Balance, string? Notes,
    string CreatedByName, DateTime CreatedAt, PettyCashEntryStatus Status);

public record CreatePettyCashRequest(
    [Required] DateTime Date, [Required, MaxLength(200)] string Particulars,
    [Range(0, 100000000)] decimal Credit, [Range(0, 100000000)] decimal Debit,
    [MaxLength(500)] string? Notes);
