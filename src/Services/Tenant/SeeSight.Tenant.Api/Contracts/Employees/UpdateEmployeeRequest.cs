namespace SeeSight.Tenant.Api.Contracts.Employees;

public sealed record UpdateEmployeeRequest(
    string FirstName,
    string LastName,
    Guid? DepartmentId,
    string? JobTitle,
    string? Phone,
    string? Nationality,
    string? PassportNumber,
    string? PreferredAirport);
