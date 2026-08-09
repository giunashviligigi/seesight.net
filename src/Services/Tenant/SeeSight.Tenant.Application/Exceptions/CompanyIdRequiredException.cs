namespace SeeSight.Tenant.Application.Exceptions;

/// <summary>Maps to 400 — a super admin must pass an explicit companyId on list/create (docs/TenantArchitecture.md §4).</summary>
public sealed class CompanyIdRequiredException() : Exception("companyId is required for this request.");
