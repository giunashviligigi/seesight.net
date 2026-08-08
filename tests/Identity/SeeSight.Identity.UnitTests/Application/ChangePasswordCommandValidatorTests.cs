using FluentAssertions;
using SeeSight.Identity.Application.Users;

namespace SeeSight.Identity.UnitTests.Application;

public sealed class ChangePasswordCommandValidatorTests
{
    private readonly ChangePasswordCommandValidator _validator = new();

    [Fact]
    public void A_well_formed_command_passes()
    {
        var result = _validator.Validate(new ChangePasswordCommand(Guid.CreateVersion7(), "CurrentPass123", "NewSecurePass123"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Empty_user_id_fails()
    {
        var result = _validator.Validate(new ChangePasswordCommand(Guid.Empty, "CurrentPass123", "NewSecurePass123"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Empty_current_password_fails()
    {
        var result = _validator.Validate(new ChangePasswordCommand(Guid.CreateVersion7(), "", "NewSecurePass123"));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("short")]
    [InlineData("")]
    public void New_password_shorter_than_8_characters_fails(string newPassword)
    {
        var result = _validator.Validate(new ChangePasswordCommand(Guid.CreateVersion7(), "CurrentPass123", newPassword));

        result.IsValid.Should().BeFalse();
    }
}
