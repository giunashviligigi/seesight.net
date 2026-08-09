using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SeeSight.SharedKernel.Http;
using SeeSight.Tenant.IntegrationTests.TestSupport;

namespace SeeSight.Tenant.IntegrationTests;

/// <summary>
/// Proves the tenant-isolation guarantee end-to-end through real HTTP requests
/// against the real EF Core query filter (docs/adr/0009-hand-rolled-tenant-context.md,
/// docs/TenantArchitecture.md §5) — this is the single most important test
/// class in M3.
/// </summary>
public sealed class TenantIsolationTests : IClassFixture<TenantApiFactory>
{
    private readonly TenantApiFactory _factory;

    public TenantIsolationTests(TenantApiFactory factory)
    {
        _factory = factory;
    }

    private static string UniqueEmail() => $"employee-{Guid.CreateVersion7()}@example.com";

    private sealed record Seed(Guid CompanyAId, Guid CompanyBId, Guid CompanyADepartmentId, Guid CompanyAEmployeeId);

    private static async Task<Seed> SeedTwoCompaniesAsync(HttpClient client)
    {
        var companyAId = await TenantSeedHelpers.CreateCompanyAsync(client);
        var companyBId = await TenantSeedHelpers.CreateCompanyAsync(client);

        var deptResponse = await client.SendAsUserAsync(HttpMethod.Post, "/departments", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, companyAId,
            body: new { companyId = (Guid?)null, name = $"Engineering-{Guid.CreateVersion7()}", code = (string?)null });
        var dept = await deptResponse.Content.ReadFromJsonAsync<JsonElement>();
        var departmentId = dept.GetProperty("id").GetGuid();

        var employeeResponse = await client.SendAsUserAsync(HttpMethod.Post, "/employees", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, companyAId,
            body: new { companyId = (Guid?)null, departmentId, email = UniqueEmail(), firstName = "First", lastName = "Last", createLogin = false });
        var employee = await employeeResponse.Content.ReadFromJsonAsync<JsonElement>();
        var employeeId = employee.GetProperty("employee").GetProperty("id").GetGuid();

        return new Seed(companyAId, companyBId, departmentId, employeeId);
    }

    [Fact]
    public async Task Company_B_admin_cannot_read_Company_As_department_by_id_via_the_list_endpoint()
    {
        using var client = _factory.CreateClient();
        var seed = await SeedTwoCompaniesAsync(client);

        // Company B's own department list must never contain Company A's department.
        var response = await client.SendAsUserAsync(HttpMethod.Get, "/departments", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, seed.CompanyBId);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("items").EnumerateArray()
            .Should().NotContain(d => d.GetProperty("id").GetGuid() == seed.CompanyADepartmentId);
    }

    [Fact]
    public async Task Company_B_admin_cannot_update_Company_As_department()
    {
        using var client = _factory.CreateClient();
        var seed = await SeedTwoCompaniesAsync(client);

        var response = await client.SendAsUserAsync(HttpMethod.Patch, $"/departments/{seed.CompanyADepartmentId}", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, seed.CompanyBId,
            body: new { name = "Hijacked", code = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, "a cross-tenant write must be indistinguishable from the resource not existing");
    }

    [Fact]
    public async Task Company_B_admin_cannot_delete_Company_As_department()
    {
        using var client = _factory.CreateClient();
        var seed = await SeedTwoCompaniesAsync(client);

        var response = await client.SendAsUserAsync(HttpMethod.Delete, $"/departments/{seed.CompanyADepartmentId}", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, seed.CompanyBId);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Company_B_admin_cannot_read_Company_As_employee_by_id()
    {
        using var client = _factory.CreateClient();
        var seed = await SeedTwoCompaniesAsync(client);

        var response = await client.SendAsUserAsync(HttpMethod.Get, $"/employees/{seed.CompanyAEmployeeId}", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, seed.CompanyBId);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Company_B_admin_cannot_update_or_deactivate_Company_As_employee()
    {
        using var client = _factory.CreateClient();
        var seed = await SeedTwoCompaniesAsync(client);

        var updateResponse = await client.SendAsUserAsync(HttpMethod.Patch, $"/employees/{seed.CompanyAEmployeeId}", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, seed.CompanyBId,
            body: new { firstName = "Hijacked", lastName = "Person", departmentId = (Guid?)null, jobTitle = (string?)null, phone = (string?)null, nationality = (string?)null, passportNumber = (string?)null, preferredAirport = (string?)null });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var deactivateResponse = await client.SendAsUserAsync(HttpMethod.Post, $"/employees/{seed.CompanyAEmployeeId}/deactivate", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, seed.CompanyBId);
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Company_B_admin_cannot_list_Company_As_employees_even_by_passing_an_explicit_companyId()
    {
        using var client = _factory.CreateClient();
        var seed = await SeedTwoCompaniesAsync(client);

        // Company B's admin explicitly tries to request Company A's employee list.
        var response = await client.SendAsUserAsync(HttpMethod.Get, $"/employees?companyId={seed.CompanyAId}", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, seed.CompanyBId);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "docs/TenantArchitecture.md §4: a non-super-admin passing a different companyId is rejected outright");
    }

    [Fact]
    public async Task Company_B_admin_cannot_read_Company_As_company_record()
    {
        using var client = _factory.CreateClient();
        var seed = await SeedTwoCompaniesAsync(client);

        var response = await client.SendAsUserAsync(HttpMethod.Get, $"/companies/{seed.CompanyAId}", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, seed.CompanyBId);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SuperAdmin_can_read_and_list_across_both_companies()
    {
        using var client = _factory.CreateClient();
        var seed = await SeedTwoCompaniesAsync(client);
        var superAdminId = Guid.CreateVersion7();

        var employeeResponse = await client.SendAsUserAsync(HttpMethod.Get, $"/employees/{seed.CompanyAEmployeeId}", superAdminId, SeeSightRoles.SuperAdmin, null);
        employeeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var departmentListResponse = await client.SendAsUserAsync(HttpMethod.Get, $"/departments?companyId={seed.CompanyAId}", superAdminId, SeeSightRoles.SuperAdmin, null);
        departmentListResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var departmentBody = await departmentListResponse.Content.ReadFromJsonAsync<JsonElement>();
        departmentBody.GetProperty("items").EnumerateArray()
            .Should().Contain(d => d.GetProperty("id").GetGuid() == seed.CompanyADepartmentId);

        var companyResponse = await client.SendAsUserAsync(HttpMethod.Get, $"/companies/{seed.CompanyAId}", superAdminId, SeeSightRoles.SuperAdmin, null);
        companyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SuperAdmin_listing_employees_must_still_supply_an_explicit_companyId()
    {
        using var client = _factory.CreateClient();
        var superAdminId = Guid.CreateVersion7();

        var response = await client.SendAsUserAsync(HttpMethod.Get, "/employees", superAdminId, SeeSightRoles.SuperAdmin, null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "docs/TenantArchitecture.md §4: no default tenant for a super admin on list/create");
    }
}
