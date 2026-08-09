using SeeSight.Tenant.Domain;

namespace SeeSight.Tenant.Application.Departments;

public sealed record DepartmentDto(
    Guid Id,
    Guid CompanyId,
    string Name,
    string? Code,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static DepartmentDto FromDomain(Department department) => new(
        department.Id,
        department.CompanyId,
        department.Name,
        department.Code,
        department.CreatedAt,
        department.UpdatedAt);
}
