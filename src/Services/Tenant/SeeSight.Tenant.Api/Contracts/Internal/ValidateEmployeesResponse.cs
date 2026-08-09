namespace SeeSight.Tenant.Api.Contracts.Internal;

public sealed record ValidateEmployeesResponse(IReadOnlyList<Guid> ValidEmployeeIds, IReadOnlyList<Guid> InvalidEmployeeIds);
