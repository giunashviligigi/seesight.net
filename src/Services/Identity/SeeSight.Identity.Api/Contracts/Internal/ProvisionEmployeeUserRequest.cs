namespace SeeSight.Identity.Api.Contracts.Internal;

public sealed record ProvisionEmployeeUserRequest(string Email, string? FirstName, string? LastName, Guid CompanyId);
