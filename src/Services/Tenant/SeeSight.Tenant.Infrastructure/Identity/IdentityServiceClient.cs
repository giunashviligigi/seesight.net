using System.Net;
using System.Net.Http.Json;
using SeeSight.Tenant.Application.Abstractions;
using SeeSight.Tenant.Application.Exceptions;

namespace SeeSight.Tenant.Infrastructure.Identity;

/// <summary>
/// The one synchronous service-to-service edge this service makes — a typed
/// <c>HttpClient</c> calling Identity Service's internal API, never a compiled
/// reference to Identity Service's code (docs/ProjectReferenceDiagram.md §6).
/// The internal-service token is attached once, at registration time (see
/// <c>InfrastructureServiceCollectionExtensions</c>), not per call.
/// </summary>
public sealed class IdentityServiceClient(HttpClient httpClient) : IIdentityServiceClient
{
    private sealed record ProvisionRequest(string Email, string? FirstName, string? LastName, Guid CompanyId);

    private sealed record ProvisionResponse(Guid UserId, string TempPassword);

    private sealed record PatchRequest(string? FirstName, string? LastName, bool ClearCompanyId, Guid? CompanyId);

    public async Task<ProvisionedUser> ProvisionEmployeeUserAsync(
        string email, string? firstName, string? lastName, Guid companyId, CancellationToken cancellationToken)
    {
        var response = await httpClient
            .PostAsJsonAsync("internal/users", new ProvisionRequest(email, firstName, lastName, companyId), cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            // Identity Service enforces a globally unique email across every
            // company (docs/DatabaseDesign.md §3) — narrower than Tenant
            // Service's own per-company uniqueness (§4). From this caller's
            // perspective both manifest identically: this email cannot be used
            // for a new login. See docs/validation/M3/README.md for the
            // documented cross-service edge case this reflects.
            throw new DuplicateEmployeeEmailException();
        }

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ProvisionResponse>(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Identity Service returned an empty response body.");

        return new ProvisionedUser(body.UserId, body.TempPassword);
    }

    public async Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var response = await httpClient.DeleteAsync($"internal/users/{userId}", cancellationToken).ConfigureAwait(false);

        // Idempotent from this caller's perspective too — the compensating
        // rollback must not itself become a new failure point.
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            response.EnsureSuccessStatusCode();
        }
    }

    public async Task DeactivateUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsync($"internal/users/{userId}/deactivate", content: null, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task ActivateUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsync($"internal/users/{userId}/activate", content: null, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateUserAsync(
        Guid userId, string? firstName, string? lastName, bool clearCompanyId, Guid? companyId, CancellationToken cancellationToken)
    {
        var response = await httpClient
            .PatchAsJsonAsync($"internal/users/{userId}", new PatchRequest(firstName, lastName, clearCompanyId, companyId), cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }
}
