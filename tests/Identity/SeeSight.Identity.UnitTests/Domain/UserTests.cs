using FluentAssertions;
using SeeSight.Identity.Domain;

namespace SeeSight.Identity.UnitTests.Domain;

public sealed class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Register_sets_the_self_signup_defaults()
    {
        var user = User.Register("someone@example.com", "hashed-password", "First", "Last", Now);

        user.Role.Should().Be(UserRole.CompanyAdmin);
        user.Status.Should().Be(UserStatus.Active);
        user.MustChangePassword.Should().BeFalse();
        user.CompanyId.Should().BeNull();
        user.CreatedAt.Should().Be(Now);
        user.UpdatedAt.Should().Be(Now);
        user.FirstName.Should().Be("First");
        user.LastName.Should().Be("Last");
    }

    [Theory]
    [InlineData("Someone@Example.com", "someone@example.com")]
    [InlineData("  spaced@example.com  ", "spaced@example.com")]
    [InlineData("MIXED.Case@EXAMPLE.com", "mixed.case@example.com")]
    public void Register_normalizes_email_to_trimmed_lowercase(string input, string expected)
    {
        var user = User.Register(input, "hashed-password", null, null, Now);

        user.Email.Should().Be(expected);
    }

    [Fact]
    public void Register_assigns_a_new_id_per_call()
    {
        var first = User.Register("a@example.com", "hash", null, null, Now);
        var second = User.Register("b@example.com", "hash", null, null, Now);

        first.Id.Should().NotBe(second.Id);
        first.Id.Should().NotBe(Guid.Empty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_throws_for_missing_email(string? email)
    {
        var act = () => User.Register(email!, "hashed-password", null, null, Now);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_throws_for_missing_password_hash(string? passwordHash)
    {
        var act = () => User.Register("someone@example.com", passwordHash!, null, null, Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CanAuthenticate_is_true_for_an_active_user()
    {
        var user = User.Register("someone@example.com", "hash", null, null, Now);

        user.CanAuthenticate.Should().BeTrue();
    }

    [Fact]
    public void ProvisionForEmployee_sets_the_admin_provisioned_defaults()
    {
        var companyId = Guid.CreateVersion7();

        var user = User.ProvisionForEmployee("someone@example.com", "hashed-temp-password", "First", "Last", companyId, Now);

        user.Role.Should().Be(UserRole.Employee);
        user.Status.Should().Be(UserStatus.Active);
        user.MustChangePassword.Should().BeTrue();
        user.CompanyId.Should().Be(companyId);
        user.FirstName.Should().Be("First");
        user.LastName.Should().Be("Last");
        user.CreatedAt.Should().Be(Now);
        user.UpdatedAt.Should().Be(Now);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ProvisionForEmployee_throws_for_missing_email(string? email)
    {
        var act = () => User.ProvisionForEmployee(email!, "hashed-temp-password", null, null, Guid.CreateVersion7(), Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Deactivate_sets_status_inactive_and_bumps_UpdatedAt()
    {
        var user = User.Register("someone@example.com", "hash", null, null, Now);

        user.Deactivate(Now.AddHours(1));

        user.Status.Should().Be(UserStatus.Inactive);
        user.CanAuthenticate.Should().BeFalse();
        user.UpdatedAt.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void Deactivate_is_idempotent()
    {
        var user = User.Register("someone@example.com", "hash", null, null, Now);
        user.Deactivate(Now.AddHours(1));

        user.Deactivate(Now.AddHours(2));

        user.UpdatedAt.Should().Be(Now.AddHours(1), "a second Deactivate call should be a no-op");
    }

    [Fact]
    public void Activate_sets_status_active_and_bumps_UpdatedAt()
    {
        var user = User.Register("someone@example.com", "hash", null, null, Now);
        user.Deactivate(Now.AddHours(1));

        user.Activate(Now.AddHours(2));

        user.Status.Should().Be(UserStatus.Active);
        user.CanAuthenticate.Should().BeTrue();
        user.UpdatedAt.Should().Be(Now.AddHours(2));
    }

    [Fact]
    public void Activate_is_idempotent()
    {
        var user = User.Register("someone@example.com", "hash", null, null, Now);

        user.Activate(Now.AddHours(1));

        user.UpdatedAt.Should().Be(Now, "the user was already active, so this should be a no-op");
    }

    [Fact]
    public void UpdateProfile_replaces_both_names_when_both_supplied()
    {
        var user = User.Register("someone@example.com", "hash", "Old", "Name", Now);

        user.UpdateProfile("New", "Person", Now.AddHours(1));

        user.FirstName.Should().Be("New");
        user.LastName.Should().Be("Person");
        user.UpdatedAt.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void UpdateProfile_leaves_a_field_unchanged_when_that_argument_is_null()
    {
        var user = User.Register("someone@example.com", "hash", "Old", "Name", Now);

        user.UpdateProfile(null, "Updated", Now.AddHours(1));

        user.FirstName.Should().Be("Old", "a null argument means leave this field unchanged");
        user.LastName.Should().Be("Updated");
    }

    [Fact]
    public void AssignToCompany_sets_the_company_id()
    {
        var user = User.Register("someone@example.com", "hash", null, null, Now);
        var companyId = Guid.CreateVersion7();

        user.AssignToCompany(companyId, Now.AddHours(1));

        user.CompanyId.Should().Be(companyId);
        user.UpdatedAt.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void AssignToCompany_with_null_clears_the_company_id()
    {
        var user = User.ProvisionForEmployee("someone@example.com", "hash", null, null, Guid.CreateVersion7(), Now);

        user.AssignToCompany(null, Now.AddHours(1));

        user.CompanyId.Should().BeNull();
    }
}
