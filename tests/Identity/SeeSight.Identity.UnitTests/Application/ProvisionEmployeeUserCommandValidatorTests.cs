using FluentAssertions;
using SeeSight.Identity.Application.Users;

namespace SeeSight.Identity.UnitTests.Application;

public sealed class ProvisionEmployeeUserCommandValidatorTests
{
    private readonly ProvisionEmployeeUserCommandValidator _validator = new();

    [Fact]
    public void A_well_formed_command_passes()
    {
        var result = _validator.Validate(new ProvisionEmployeeUserCommand("someone@example.com", "First", "Last", Guid.CreateVersion7()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Invalid_email_fails()
    {
        var result = _validator.Validate(new ProvisionEmployeeUserCommand("not-an-email", null, null, Guid.CreateVersion7()));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Empty_company_id_fails()
    {
        var result = _validator.Validate(new ProvisionEmployeeUserCommand("someone@example.com", null, null, Guid.Empty));

        result.IsValid.Should().BeFalse();
    }
}
