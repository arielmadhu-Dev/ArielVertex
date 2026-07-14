using System.ComponentModel.DataAnnotations;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Application.Contracts;

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record MicrosoftLoginRequest([Required] string Token);

public record AuthResponse(string Token, DateTime ExpiresAt, CurrentUserDto User);

public record CurrentUserDto(
    int Id, string Name, string Email, string EmployeeCode, PortalRole Role, string RoleLabel,
    string Designation, string? Department, string AvatarColor, string Dashboard,
    IReadOnlyCollection<string> Permissions);
