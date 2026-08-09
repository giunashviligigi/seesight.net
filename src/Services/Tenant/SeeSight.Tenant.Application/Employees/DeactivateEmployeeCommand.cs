using MediatR;

namespace SeeSight.Tenant.Application.Employees;

public sealed record DeactivateEmployeeCommand(Guid Id) : IRequest;
