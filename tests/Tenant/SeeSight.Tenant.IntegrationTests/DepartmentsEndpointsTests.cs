using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SeeSight.SharedKernel.Http;
using SeeSight.Tenant.IntegrationTests.TestSupport;

namespace SeeSight.Tenant.IntegrationTests;

public sealed class DepartmentsEndpointsTests : IClassFixture<TenantApiFactory>
{
    private readonly TenantApiFactory _factory;

    public DepartmentsEndpointsTests(TenantApiFactory factory)
    {
        _factory = factory;
    }

    private static string UniqueName() => $"Engineering-{Guid.CreateVersion7()}";

    [Fact]
    public async Task Create_then_list_returns_the_department_for_the_callers_company()
    {
        using var client = _factory.CreateClient();
        var companyId = await TenantSeedHelpers.CreateCompanyAsync(client);
        var name = UniqueName();

        var createResponse = await client.SendAsUserAsync(HttpMethod.Post, "/departments", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, companyId,
            body: new { companyId = (Guid?)null, name, code = "ENG" });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await client.SendAsUserAsync(HttpMethod.Get, "/departments", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, companyId);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").EnumerateArray().Should().Contain(e => e.GetProperty("name").GetString() == name);
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_name_within_the_same_company()
    {
        using var client = _factory.CreateClient();
        var companyId = await TenantSeedHelpers.CreateCompanyAsync(client);
        var name = UniqueName();
        var payload = new { companyId = (Guid?)null, name, code = (string?)null };

        var first = await client.SendAsUserAsync(HttpMethod.Post, "/departments", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, companyId, payload);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.SendAsUserAsync(HttpMethod.Post, "/departments", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, companyId, payload);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_unassigns_members_instead_of_deleting_them()
    {
        using var client = _factory.CreateClient();
        var companyId = await TenantSeedHelpers.CreateCompanyAsync(client);

        var deptResponse = await client.SendAsUserAsync(HttpMethod.Post, "/departments", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, companyId,
            body: new { companyId = (Guid?)null, name = UniqueName(), code = (string?)null });
        var dept = await deptResponse.Content.ReadFromJsonAsync<JsonElement>();
        var departmentId = dept.GetProperty("id").GetGuid();

        var employeeResponse = await client.SendAsUserAsync(HttpMethod.Post, "/employees", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, companyId,
            body: new
            {
                companyId = (Guid?)null,
                departmentId,
                email = $"member-{Guid.CreateVersion7()}@example.com",
                firstName = "First",
                lastName = "Last",
                createLogin = false,
            });
        var employeeBody = await employeeResponse.Content.ReadFromJsonAsync<JsonElement>();
        var employeeId = employeeBody.GetProperty("employee").GetProperty("id").GetGuid();
        employeeBody.GetProperty("employee").GetProperty("departmentId").GetGuid().Should().Be(departmentId);

        var deleteResponse = await client.SendAsUserAsync(HttpMethod.Delete, $"/departments/{departmentId}", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, companyId);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getEmployeeResponse = await client.SendAsUserAsync(HttpMethod.Get, $"/employees/{employeeId}", Guid.CreateVersion7(), SeeSightRoles.CompanyAdmin, companyId);
        var getEmployeeBody = await getEmployeeResponse.Content.ReadFromJsonAsync<JsonElement>();
        getEmployeeBody.GetProperty("departmentId").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
