using MediatR;

namespace SeeSight.Tenant.Application.Companies;

public sealed record ActivateCompanyCommand(Guid Id) : IRequest;
