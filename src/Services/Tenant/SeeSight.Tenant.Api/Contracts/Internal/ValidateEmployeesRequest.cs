namespace SeeSight.Tenant.Api.Contracts.Internal;

public sealed record ValidateEmployeesRequest(Guid CompanyId, IReadOnlyList<Guid> EmployeeIds);
