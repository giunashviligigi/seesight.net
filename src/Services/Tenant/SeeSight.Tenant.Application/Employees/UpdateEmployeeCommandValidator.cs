using FluentValidation;

namespace SeeSight.Tenant.Application.Employees;

public sealed class UpdateEmployeeCommandValidator : AbstractValidator<UpdateEmployeeCommand>
{
    public UpdateEmployeeCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(c => c.LastName).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Nationality).Length(2).When(c => c.Nationality is not null);
        RuleFor(c => c.PreferredAirport).Length(3).When(c => c.PreferredAirport is not null);
    }
}
