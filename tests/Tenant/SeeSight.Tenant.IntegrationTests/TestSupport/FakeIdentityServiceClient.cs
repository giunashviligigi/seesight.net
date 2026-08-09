using SeeSight.Tenant.Application.Abstractions;

namespace SeeSight.Tenant.IntegrationTests.TestSupport;

/// <summary>
/// Fakes the one external service call this service makes — Identity Service
/// is never called live from tests, per docs/CodingStandards.md §5. Records
/// every call so tests can assert on the compensating-rollback and
/// lifecycle-sync behavior without a real Identity Service running.
/// </summary>
public sealed class FakeIdentityServiceClient : IIdentityServiceClient
{
    public List<(string Email, string? FirstName, string? LastName, Guid CompanyId)> ProvisionCalls { get; } = [];

    public List<Guid> DeleteCalls { get; } = [];

    public List<Guid> DeactivateCalls { get; } = [];

    public List<Guid> ActivateCalls { get; } = [];

    public List<(Guid UserId, string? FirstName, string? LastName, bool ClearCompanyId, Guid? CompanyId)> UpdateCalls { get; } = [];

    /// <summary>Set to force the next ProvisionEmployeeUserAsync call to throw — simulates an Identity Service failure.</summary>
    public Exception? ThrowOnProvision { get; set; }

    public Task<ProvisionedUser> ProvisionEmployeeUserAsync(string email, string? firstName, string? lastName, Guid companyId, CancellationToken cancellationToken)
    {
        if (ThrowOnProvision is not null)
        {
            return Task.FromException<ProvisionedUser>(ThrowOnProvision);
        }

        ProvisionCalls.Add((email, firstName, lastName, companyId));
        return Task.FromResult(new ProvisionedUser(Guid.CreateVersion7(), $"temp-{Guid.CreateVersion7():N}"));
    }

    public Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        DeleteCalls.Add(userId);
        return Task.CompletedTask;
    }

    public Task DeactivateUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        DeactivateCalls.Add(userId);
        return Task.CompletedTask;
    }

    public Task ActivateUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        ActivateCalls.Add(userId);
        return Task.CompletedTask;
    }

    public Task UpdateUserAsync(Guid userId, string? firstName, string? lastName, bool clearCompanyId, Guid? companyId, CancellationToken cancellationToken)
    {
        UpdateCalls.Add((userId, firstName, lastName, clearCompanyId, companyId));
        return Task.CompletedTask;
    }
}
