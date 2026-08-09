using MediatR;

namespace SeeSight.Tenant.Application.Companies;

public sealed record DeleteCompanyCommand(Guid Id) : IRequest;
