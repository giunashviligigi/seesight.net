using MediatR;

namespace SeeSight.Identity.Application.Users;

/// <summary>
/// Syncs a linked user's profile from its owning Employee record
/// (<c>PATCH /employees/{id}</c>), and/or assigns or clears its company link
/// (Company Service's assign-admin/unassign-admin flow). <see cref="FirstName"/>/
/// <see cref="LastName"/> of <see langword="null"/> mean "leave unchanged."
/// <see cref="ClearCompanyId"/> explicitly clears the company link; otherwise a
/// non-null <see cref="CompanyId"/> sets it, and both left unset means "leave
/// unchanged" — see <c>User.UpdateProfile</c>/<c>User.AssignToCompany</c>.
/// </summary>
public sealed record UpdateInternalUserCommand(
    Guid UserId,
    string? FirstName,
    string? LastName,
    bool ClearCompanyId,
    Guid? CompanyId) : IRequest;
