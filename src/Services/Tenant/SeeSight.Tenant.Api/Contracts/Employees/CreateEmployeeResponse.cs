using SeeSight.Tenant.Application.Employees;

namespace SeeSight.Tenant.Api.Contracts.Employees;

/// <summary><see cref="TempPassword"/> is populated only when the request set <c>createLogin: true</c> — docs/TenantArchitecture.md §6.</summary>
public sealed record CreateEmployeeResponse(EmployeeDto Employee, string? TempPassword)
{
    public static CreateEmployeeResponse FromResult(CreateEmployeeResult result) => new(result.Employee, result.TempPassword);
}
