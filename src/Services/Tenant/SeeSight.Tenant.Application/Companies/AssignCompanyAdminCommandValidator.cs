using FluentValidation;

namespace SeeSight.Tenant.Application.Companies;

public sealed class AssignCompanyAdminCommandValidator : AbstractValidator<AssignCompanyAdminCommand>
{
    public AssignCompanyAdminCommandValidator()
    {
        RuleFor(c => c.CompanyId).NotEmpty();
        RuleFor(c => c.UserId).NotEmpty();
    }
}
