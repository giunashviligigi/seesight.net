namespace SeeSight.Tenant.Api.Contracts.Companies;

public sealed record CreateCompanyRequest(
    string Name,
    string? LegalName,
    string? Country,
    string? BillingEmail,
    string Timezone,
    string? PolicyJson);
