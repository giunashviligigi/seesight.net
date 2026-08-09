namespace SeeSight.Tenant.Api.Contracts.Departments;

public sealed record CreateDepartmentRequest(Guid? CompanyId, string Name, string? Code);
