namespace SeeSight.Tenant.Application.Exceptions;

/// <summary>Maps to 404. Also the result of an EMPLOYEE self-scope violation on GET /employees/{id} — deliberately indistinguishable from "doesn't exist," matching the tenant-isolation pattern (docs/Authorization.md §4).</summary>
public sealed class EmployeeNotFoundException() : Exception("Employee not found.");
