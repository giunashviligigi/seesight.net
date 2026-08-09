using MediatR;

namespace SeeSight.Tenant.Application.Companies;

public sealed record DeactivateCompanyCommand(Guid Id) : IRequest;
