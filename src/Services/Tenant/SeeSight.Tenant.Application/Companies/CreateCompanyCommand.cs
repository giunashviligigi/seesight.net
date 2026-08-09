using MediatR;

namespace SeeSight.Tenant.Application.Companies;

public sealed record CreateCompanyCommand(
    string Name,
    string? LegalName,
    string? Country,
    string? BillingEmail,
    string Timezone,
    string? PolicyJson) : IRequest<CompanyDto>;
