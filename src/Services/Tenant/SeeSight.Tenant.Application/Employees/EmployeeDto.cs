using SeeSight.Tenant.Domain;

namespace SeeSight.Tenant.Application.Employees;

public sealed record EmployeeDto(
    Guid Id,
    Guid CompanyId,
    Guid? DepartmentId,
    Guid? UserId,
    string Email,
    string FirstName,
    string LastName,
    string? JobTitle,
    string? Phone,
    string? Nationality,
    string? PassportNumber,
    string? PreferredAirport,
    EmployeeStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static EmployeeDto FromDomain(Employee employee) => new(
        employee.Id,
        employee.CompanyId,
        employee.DepartmentId,
        employee.UserId,
        employee.Email,
        employee.FirstName,
        employee.LastName,
        employee.JobTitle,
        employee.Phone,
        employee.Nationality,
        employee.PassportNumber,
        employee.PreferredAirport,
        employee.Status,
        employee.CreatedAt,
        employee.UpdatedAt);
}
