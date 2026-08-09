using FluentAssertions;
using SeeSight.Identity.Application.Users;

namespace SeeSight.Identity.UnitTests.Application;

public sealed class UpdateInternalUserCommandValidatorTests
{
    private readonly UpdateInternalUserCommandValidator _validator = new();

    [Fact]
    public void A_well_formed_command_passes()
    {
        var result = _validator.Validate(new UpdateInternalUserCommand(Guid.CreateVersion7(), "First", "Last", false, Guid.CreateVersion7()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Empty_user_id_fails()
    {
        var result = _validator.Validate(new UpdateInternalUserCommand(Guid.Empty, "First", null, false, null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Supplying_both_ClearCompanyId_and_a_CompanyId_fails()
    {
        var result = _validator.Validate(new UpdateInternalUserCommand(Guid.CreateVersion7(), null, null, true, Guid.CreateVersion7()));

        result.IsValid.Should().BeFalse();
    }
}
