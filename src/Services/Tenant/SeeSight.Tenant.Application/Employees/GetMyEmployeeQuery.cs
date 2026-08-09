using MediatR;

namespace SeeSight.Tenant.Application.Employees;

public sealed record GetMyEmployeeQuery : IRequest<EmployeeDto>;
