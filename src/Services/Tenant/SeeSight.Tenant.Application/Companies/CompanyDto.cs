using SeeSight.Tenant.Domain;

namespace SeeSight.Tenant.Application.Companies;

public sealed record CompanyDto(
    Guid Id,
    string Name,
    string? LegalName,
    string Slug,
    string? Country,
    string? BillingEmail,
    string Timezone,
    CompanyStatus Status,
    string? PolicyJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static CompanyDto FromDomain(Company company) => new(
        company.Id,
        company.Name,
        company.LegalName,
        company.Slug,
        company.Country,
        company.BillingEmail,
        company.Timezone,
        company.Status,
        company.PolicyJson,
        company.CreatedAt,
        company.UpdatedAt);
}
