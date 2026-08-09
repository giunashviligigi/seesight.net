using MediatR;

namespace SeeSight.Tenant.Application.Employees;

public sealed record UpdateEmployeeCommand(
    Guid Id,
    string FirstName,
    string LastName,
    Guid? DepartmentId,
    string? JobTitle,
    string? Phone,
    string? Nationality,
    string? PassportNumber,
    string? PreferredAirport) : IRequest<EmployeeDto>;
