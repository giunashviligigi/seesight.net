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
}
