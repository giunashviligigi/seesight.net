using MediatR;

namespace SeeSight.Tenant.Application.Departments;

public sealed record UpdateDepartmentCommand(Guid Id, string Name, string? Code) : IRequest<DepartmentDto>;
