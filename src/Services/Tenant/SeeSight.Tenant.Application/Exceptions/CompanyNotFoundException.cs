namespace SeeSight.Tenant.Application.Exceptions;

/// <summary>Maps to 404. Also the result of a cross-tenant single-resource read hitting the EF Core tenant filter — never distinguishable from "genuinely doesn't exist," per docs/TenantArchitecture.md §5.</summary>
public sealed class CompanyNotFoundException() : Exception("Company not found.");
