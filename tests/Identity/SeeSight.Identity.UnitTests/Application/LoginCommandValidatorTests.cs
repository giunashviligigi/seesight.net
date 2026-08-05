using FluentAssertions;
using SeeSight.Identity.Application.Users;

namespace SeeSight.Identity.UnitTests.Application;

public sealed class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void A_well_formed_command_passes()
    {
        var result = _validator.Validate(new LoginCommand("someone@example.com", "any-password"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Empty_email_fails()
    {
        var result = _validator.Validate(new LoginCommand("", "any-password"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Empty_password_fails()
    {
        var result = _validator.Validate(new LoginCommand("someone@example.com", ""));

        result.IsValid.Should().BeFalse();
    }
}
