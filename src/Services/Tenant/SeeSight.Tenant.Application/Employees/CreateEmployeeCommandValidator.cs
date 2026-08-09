using FluentValidation;

namespace SeeSight.Tenant.Application.Employees;

public sealed class CreateEmployeeCommandValidator : AbstractValidator<CreateEmployeeCommand>
{
    public CreateEmployeeCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
        RuleFor(c => c.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(c => c.LastName).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Nationality).Length(2).When(c => c.Nationality is not null);
        RuleFor(c => c.PreferredAirport).Length(3).When(c => c.PreferredAirport is not null);
    }
}
