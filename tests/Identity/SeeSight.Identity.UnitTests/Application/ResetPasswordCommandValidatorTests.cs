using FluentAssertions;
using SeeSight.Identity.Application.Users;

namespace SeeSight.Identity.UnitTests.Application;

public sealed class ResetPasswordCommandValidatorTests
{
    private readonly ResetPasswordCommandValidator _validator = new();

    [Fact]
    public void A_well_formed_command_passes()
    {
        var result = _validator.Validate(new ResetPasswordCommand("some-reset-token", "NewSecurePass123"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Empty_token_fails()
    {
        var result = _validator.Validate(new ResetPasswordCommand("", "NewSecurePass123"));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("short")]
    [InlineData("")]
    public void Password_shorter_than_8_characters_fails(string password)
    {
        var result = _validator.Validate(new ResetPasswordCommand("some-reset-token", password));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Password_longer_than_200_characters_fails()
    {
        var result = _validator.Validate(new ResetPasswordCommand("some-reset-token", new string('a', 201)));

        result.IsValid.Should().BeFalse();
    }
}
