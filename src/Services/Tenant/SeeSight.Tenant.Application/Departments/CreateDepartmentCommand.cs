using MediatR;

namespace SeeSight.Tenant.Application.Departments;

public sealed record CreateDepartmentCommand(Guid? CompanyId, string Name, string? Code) : IRequest<DepartmentDto>;
