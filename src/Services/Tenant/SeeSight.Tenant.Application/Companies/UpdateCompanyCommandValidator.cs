using FluentValidation;

namespace SeeSight.Tenant.Application.Companies;

public sealed class UpdateCompanyCommandValidator : AbstractValidator<UpdateCompanyCommand>
{
    public UpdateCompanyCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Timezone).NotEmpty().MaximumLength(100);
        RuleFor(c => c.BillingEmail).EmailAddress().When(c => c.BillingEmail is not null);
        RuleFor(c => c.Country).Length(2).When(c => c.Country is not null);
    }
}
