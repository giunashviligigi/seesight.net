using FluentValidation;

namespace SeeSight.Tenant.Application.Employees;

public sealed class ValidateEmployeesQueryValidator : AbstractValidator<ValidateEmployeesQuery>
{
    public ValidateEmployeesQueryValidator()
    {
        RuleFor(q => q.CompanyId).NotEmpty();
        RuleFor(q => q.EmployeeIds).NotEmpty();
    }
}
