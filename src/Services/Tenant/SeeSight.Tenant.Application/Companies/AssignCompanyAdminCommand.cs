using MediatR;

namespace SeeSight.Tenant.Application.Companies;

public sealed record AssignCompanyAdminCommand(Guid CompanyId, Guid UserId, bool ReplaceExisting) : IRequest;
