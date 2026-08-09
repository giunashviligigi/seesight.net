using MediatR;

namespace SeeSight.Tenant.Application.Companies;

public sealed record GetMyCompanyQuery : IRequest<CompanyDto>;
