namespace SeeSight.Identity.Api.Contracts.Internal;

/// <summary>
/// <see cref="FirstName"/>/<see cref="LastName"/> of <see langword="null"/> mean
/// "leave unchanged." <see cref="ClearCompanyId"/> explicitly clears the company
/// link; a non-null <see cref="CompanyId"/> sets it; both unset means "leave
/// unchanged."
/// </summary>
public sealed record PatchInternalUserRequest(string? FirstName, string? LastName, bool ClearCompanyId, Guid? CompanyId);
