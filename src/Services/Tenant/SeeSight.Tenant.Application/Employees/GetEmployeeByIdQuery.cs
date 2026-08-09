using MediatR;

namespace SeeSight.Tenant.Application.Employees;

/// <summary>An EMPLOYEE is restricted to their own record — docs/APIContracts.md.</summary>
public sealed record GetEmployeeByIdQuery(Guid Id) : IRequest<EmployeeDto>;
