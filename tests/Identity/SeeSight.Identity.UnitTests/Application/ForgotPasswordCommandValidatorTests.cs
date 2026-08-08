using FluentAssertions;
using SeeSight.Identity.Application.Users;

namespace SeeSight.Identity.UnitTests.Application;

public sealed class ForgotPasswordCommandValidatorTests
{
    private readonly ForgotPasswordCommandValidator _validator = new();

    [Fact]
    public void A_well_formed_command_passes()
    {
        var result = _validator.Validate(new ForgotPasswordCommand("someone@example.com"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Empty_email_fails()
    {
        var result = _validator.Validate(new ForgotPasswordCommand(""));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Malformed_email_fails()
    {
        var result = _validator.Validate(new ForgotPasswordCommand("not-an-email"));

        result.IsValid.Should().BeFalse();
    }
}
