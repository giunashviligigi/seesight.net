using MediatR;

namespace SeeSight.Tenant.Application.Companies;

public sealed record UpdateCompanyCommand(
    Guid Id,
    string Name,
    string? LegalName,
    string? Country,
    string? BillingEmail,
    string Timezone,
    string? PolicyJson) : IRequest<CompanyDto>;
