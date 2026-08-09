namespace SeeSight.Tenant.Application.Exceptions;

/// <summary>Maps to 403 — a non-super-admin caller with no company assigned yet.</summary>
public sealed class NoCompanyAssignedException() : Exception("No company is assigned to this account.");
