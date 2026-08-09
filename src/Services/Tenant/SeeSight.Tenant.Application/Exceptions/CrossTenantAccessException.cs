namespace SeeSight.Tenant.Application.Exceptions;

/// <summary>Maps to 403 — a non-super-admin explicitly requested a different company's data (docs/TenantArchitecture.md §4).</summary>
public sealed class CrossTenantAccessException() : Exception("You cannot access another company's data.");
