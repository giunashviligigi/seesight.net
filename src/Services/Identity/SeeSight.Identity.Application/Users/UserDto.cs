using SeeSight.Identity.Domain;

namespace SeeSight.Identity.Application.Users;

public sealed record UserDto(
    Guid Id,
    string Email,
    string? FirstName,
    string? LastName,
    UserRole Role,
    UserStatus Status,
    bool MustChangePassword,
    Guid? CompanyId)
{
    public static UserDto FromDomain(User user) => new(
        user.Id,
        user.Email,
        user.FirstName,
        user.LastName,
        user.Role,
        user.Status,
        user.MustChangePassword,
        user.CompanyId);
}
