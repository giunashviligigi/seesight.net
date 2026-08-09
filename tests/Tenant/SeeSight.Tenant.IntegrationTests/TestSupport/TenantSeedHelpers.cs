using System.Net.Http.Json;
using System.Text.Json;
using SeeSight.SharedKernel.Http;

namespace SeeSight.Tenant.IntegrationTests.TestSupport;

/// <summary>
/// Department/Employee.CompanyId carries a real FK to companies (same
/// database, not a cross-service reference — docs/DatabaseDesign.md's Tenant
/// Service ERD), so every test that creates a department or employee under a
/// companyId needs an actual Company row first, not just an arbitrary Guid.
/// </summary>
internal static class TenantSeedHelpers
{
    public static async Task<Guid> CreateCompanyAsync(HttpClient client)
    {
        var response = await client.SendAsUserAsync(HttpMethod.Post, "/companies", Guid.CreateVersion7(), SeeSightRoles.SuperAdmin, null,
            body: new { name = $"Company-{Guid.CreateVersion7()}", legalName = (string?)null, country = (string?)null, billingEmail = (string?)null, timezone = "UTC", policyJson = (string?)null });
        var company = await response.Content.ReadFromJsonAsync<JsonElement>();
        return company.GetProperty("id").GetGuid();
    }
}
