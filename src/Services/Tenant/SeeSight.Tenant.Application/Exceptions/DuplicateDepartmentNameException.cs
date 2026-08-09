namespace SeeSight.Tenant.Application.Exceptions;

/// <summary>Maps to 409 — unique per (CompanyId, Name), docs/DatabaseDesign.md §4.</summary>
public sealed class DuplicateDepartmentNameException() : Exception("A department with this name already exists.");
