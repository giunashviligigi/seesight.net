using FluentAssertions;
using SeeSight.Identity.Application.Users;

namespace SeeSight.Identity.UnitTests.Application;

public sealed class RefreshTokenCommandValidatorTests
{
    private readonly RefreshTokenCommandValidator _validator = new();

    [Fact]
    public void A_well_formed_command_passes()
    {
        var result = _validator.Validate(new RefreshTokenCommand("some-refresh-token"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Empty_refresh_token_fails()
    {
        var result = _validator.Validate(new RefreshTokenCommand(""));

        result.IsValid.Should().BeFalse();
    }
}
