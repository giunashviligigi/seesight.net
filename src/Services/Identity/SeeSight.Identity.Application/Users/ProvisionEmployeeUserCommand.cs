using MediatR;

namespace SeeSight.Identity.Application.Users;

/// <summary>
/// Admin-provisioned employee login — called by Tenant Service's internal API
/// as part of <c>POST /employees</c> with <c>createLogin: true</c>, per
/// docs/TenantArchitecture.md §6.
/// </summary>
public sealed record ProvisionEmployeeUserCommand(
    string Email,
    string? FirstName,
    string? LastName,
    Guid CompanyId) : IRequest<ProvisionEmployeeUserResult>;

public sealed record ProvisionEmployeeUserResult(Guid UserId, string TempPassword);
