using MediatR;

namespace SeeSight.Tenant.Application.Employees;

public sealed record CreateEmployeeCommand(
    Guid? CompanyId,
    Guid? DepartmentId,
    string Email,
    string FirstName,
    string LastName,
    string? JobTitle,
    string? Phone,
    string? Nationality,
    string? PassportNumber,
    string? PreferredAirport,
    bool CreateLogin) : IRequest<CreateEmployeeResult>;

/// <summary><see cref="TempPassword"/> is populated only when <see cref="CreateEmployeeCommand.CreateLogin"/> was set — docs/TenantArchitecture.md §6.</summary>
public sealed record CreateEmployeeResult(EmployeeDto Employee, string? TempPassword);
