namespace SeeSight.Tenant.Application.Exceptions;

/// <summary>Maps to 409 — a COMPANY_ADMIN may self-create a company only while they have none (docs/Authorization.md §5).</summary>
public sealed class CompanyAlreadyAssignedException() : Exception("This account already has a company assigned.");
