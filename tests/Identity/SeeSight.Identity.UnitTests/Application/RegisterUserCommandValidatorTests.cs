using FluentAssertions;
using SeeSight.Identity.Application.Users;

namespace SeeSight.Identity.UnitTests.Application;

public sealed class RegisterUserCommandValidatorTests
{
    private readonly RegisterUserCommandValidator _validator = new();

    [Fact]
    public void A_well_formed_command_passes()
    {
        var result = _validator.Validate(new RegisterUserCommand("someone@example.com", "SecurePass123", "First", "Last"));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("missing-domain@")]
    public void Invalid_email_fails(string email)
    {
        var result = _validator.Validate(new RegisterUserCommand(email, "SecurePass123", null, null));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    public void Password_shorter_than_8_characters_fails(string password)
    {
        var result = _validator.Validate(new RegisterUserCommand("someone@example.com", password, null, null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Null_first_and_last_name_are_allowed()
    {
        var result = _validator.Validate(new RegisterUserCommand("someone@example.com", "SecurePass123", null, null));

        result.IsValid.Should().BeTrue();
    }
}
