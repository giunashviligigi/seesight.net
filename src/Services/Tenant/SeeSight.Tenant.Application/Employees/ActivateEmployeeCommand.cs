using MediatR;

namespace SeeSight.Tenant.Application.Employees;

public sealed record ActivateEmployeeCommand(Guid Id) : IRequest;
