using MediatR;

namespace SeeSight.Tenant.Application.Employees;

public sealed record DeleteEmployeeCommand(Guid Id) : IRequest;
