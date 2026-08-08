using FluentValidation;

namespace SeeSight.Identity.Application.Users;

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
    }
}
