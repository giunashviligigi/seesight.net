using FluentValidation;

namespace SeeSight.Identity.Application.Users;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(c => c.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleFor(c => c.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(200);

        RuleFor(c => c.FirstName).MaximumLength(200);
        RuleFor(c => c.LastName).MaximumLength(200);
    }
}
