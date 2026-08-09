using MediatR;

namespace SeeSight.Tenant.Application.Departments;

public sealed record DeleteDepartmentCommand(Guid Id) : IRequest;
