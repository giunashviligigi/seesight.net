namespace SeeSight.Tenant.Api.Contracts.Employees;

public sealed record CreateEmployeeRequest(
    Guid? CompanyId,
    Guid? DepartmentId,
    string Email,
    string FirstName,
    string LastName,
    string? JobTitle,
    string? Phone,
    string? Nationality,
    string? PassportNumber,
    string? PreferredAirport,
    bool CreateLogin);
