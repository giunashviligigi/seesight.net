using MediatR;
using SeeSight.Shared.Common;

namespace SeeSight.Tenant.Application.Departments;

public sealed record GetDepartmentsQuery(Guid? CompanyId) : IRequest<PagedResult<DepartmentDto>>;
