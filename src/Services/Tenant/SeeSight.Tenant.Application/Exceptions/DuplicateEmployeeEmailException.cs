namespace SeeSight.Tenant.Application.Exceptions;

/// <summary>Maps to 409 — unique per (CompanyId, Name), docs/DatabaseDesign.md §4. The same email may exist under a different tenant.</summary>
public sealed class DuplicateEmployeeEmailException() : Exception("An employee with this email already exists in this company.");
