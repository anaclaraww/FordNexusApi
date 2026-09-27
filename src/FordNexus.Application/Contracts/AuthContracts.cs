using System.ComponentModel.DataAnnotations;

namespace FordNexus.Application.Contracts;

public sealed class LoginRequest
{
    [Required, EmailAddress, StringLength(160)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(128), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

public sealed record UserResponse(
    Guid Id,
    string Name,
    string Email,
    string Role,
    Guid? DealershipId,
    Guid? WorkshopId,
    DateTime CreatedAt);

public sealed record TokenResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    DateTime ExpiresAt,
    UserResponse User);

public sealed class CreateUserRequest
{
    [Required, StringLength(120, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(160)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 12), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public Domain.Enums.UserRole? Role { get; set; }

    public Guid? DealershipId { get; set; }

    public Guid? WorkshopId { get; set; }
}
