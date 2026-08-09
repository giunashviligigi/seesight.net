namespace SeeSight.Tenant.Api.Contracts.Companies;

public sealed record AssignCompanyAdminRequest(Guid UserId, bool ReplaceExisting);
