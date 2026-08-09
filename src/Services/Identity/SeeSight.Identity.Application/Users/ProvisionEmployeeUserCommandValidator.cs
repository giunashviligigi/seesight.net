using FluentValidation;

namespace SeeSight.Identity.Application.Users;

public sealed class ProvisionEmployeeUserCommandValidator : AbstractValidator<ProvisionEmployeeUserCommand>
{
    public ProvisionEmployeeUserCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
        RuleFor(c => c.CompanyId).NotEmpty();
    }
}
