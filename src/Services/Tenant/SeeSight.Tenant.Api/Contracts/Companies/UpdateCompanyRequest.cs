namespace SeeSight.Tenant.Api.Contracts.Companies;

public sealed record UpdateCompanyRequest(
    string Name,
    string? LegalName,
    string? Country,
    string? BillingEmail,
    string Timezone,
    string? PolicyJson);
