using FluentAssertions;
using SeeSight.Tenant.Application.Employees;

namespace SeeSight.Tenant.UnitTests.Application;

public sealed class CreateEmployeeCommandValidatorTests
{
    private readonly CreateEmployeeCommandValidator _validator = new();

    private static readonly CreateEmployeeCommand WellFormed = new(
        Guid.CreateVersion7(), null, "someone@example.com", "First", "Last", null, null, "US", null, "JFK", false);

    [Fact]
    public void A_well_formed_command_passes()
    {
        var result = _validator.Validate(WellFormed);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Invalid_email_fails()
    {
        var result = _validator.Validate(WellFormed with { Email = "not-an-email" });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Empty_first_name_fails()
    {
        var result = _validator.Validate(WellFormed with { FirstName = "" });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Nationality_must_be_a_2_letter_code()
    {
        var result = _validator.Validate(WellFormed with { Nationality = "USA" });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void PreferredAirport_must_be_a_3_letter_code()
    {
        var result = _validator.Validate(WellFormed with { PreferredAirport = "JFK1" });

        result.IsValid.Should().BeFalse();
    }
}
