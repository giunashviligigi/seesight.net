using FluentValidation;

namespace SeeSight.Identity.Application.Users;

public sealed class UpdateInternalUserCommandValidator : AbstractValidator<UpdateInternalUserCommand>
{
    public UpdateInternalUserCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c)
            .Must(c => !c.ClearCompanyId || c.CompanyId is null)
            .WithMessage("CompanyId must not be supplied when ClearCompanyId is set.");
    }
}
