namespace SeeSight.Tenant.Application.Abstractions;

public sealed record ProvisionedUser(Guid UserId, string TempPassword);

/// <summary>
/// The one synchronous service-to-service edge this service makes — Tenant
/// Service calling Identity Service's internal API (docs/TenantArchitecture.md §6),
/// carrying the shared internal-service token
/// (docs/adr/0006-internal-service-to-service-authentication.md). Defined here
/// (Application), implemented in Infrastructure as a typed <c>HttpClient</c> —
/// a network client calling a URL + shared DTO contract, never a compiled
/// reference to Identity Service's code (docs/ProjectReferenceDiagram.md §6).
/// </summary>
public interface IIdentityServiceClient
{
    Task<ProvisionedUser> ProvisionEmployeeUserAsync(
        string email, string? firstName, string? lastName, Guid companyId, CancellationToken cancellationToken);

    /// <summary>Reserved for the createLogin compensating-rollback path — see docs/TenantArchitecture.md §6.</summary>
    Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken);

    Task DeactivateUserAsync(Guid userId, CancellationToken cancellationToken);

    Task ActivateUserAsync(Guid userId, CancellationToken cancellationToken);

    Task UpdateUserAsync(
        Guid userId, string? firstName, string? lastName, bool clearCompanyId, Guid? companyId, CancellationToken cancellationToken);
}
