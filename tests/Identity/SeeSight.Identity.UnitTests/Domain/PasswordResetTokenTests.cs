using FluentAssertions;
using SeeSight.Identity.Domain;

namespace SeeSight.Identity.UnitTests.Domain;

public sealed class PasswordResetTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.CreateVersion7();

    [Fact]
    public void Issue_sets_the_expected_fields()
    {
        var expiresAt = Now.AddHours(1);

        var token = PasswordResetToken.Issue(UserId, "token-hash", expiresAt, Now);

        token.Id.Should().NotBe(Guid.Empty);
        token.UserId.Should().Be(UserId);
        token.TokenHash.Should().Be("token-hash");
        token.ExpiresAt.Should().Be(expiresAt);
        token.CreatedAt.Should().Be(Now);
        token.UsedAt.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Issue_throws_for_missing_token_hash(string? tokenHash)
    {
        var act = () => PasswordResetToken.Issue(UserId, tokenHash!, Now.AddHours(1), Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Issue_throws_when_expiry_is_not_in_the_future()
    {
        var act = () => PasswordResetToken.Issue(UserId, "token-hash", Now, Now);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void IsValid_is_true_for_a_fresh_unused_token()
    {
        var token = PasswordResetToken.Issue(UserId, "token-hash", Now.AddHours(1), Now);

        token.IsValid(Now).Should().BeTrue();
    }

    [Fact]
    public void IsValid_is_false_once_expired()
    {
        var token = PasswordResetToken.Issue(UserId, "token-hash", Now.AddHours(1), Now);

        token.IsValid(Now.AddHours(2)).Should().BeFalse();
    }

    [Fact]
    public void IsValid_is_false_once_used()
    {
        var token = PasswordResetToken.Issue(UserId, "token-hash", Now.AddHours(1), Now);

        token.MarkUsed(Now.AddMinutes(5));

        token.IsValid(Now.AddMinutes(6)).Should().BeFalse();
    }

    [Fact]
    public void MarkUsed_sets_UsedAt()
    {
        var token = PasswordResetToken.Issue(UserId, "token-hash", Now.AddHours(1), Now);

        token.MarkUsed(Now.AddMinutes(5));

        token.UsedAt.Should().Be(Now.AddMinutes(5));
    }

    [Fact]
    public void MarkUsed_throws_on_a_second_call()
    {
        var token = PasswordResetToken.Issue(UserId, "token-hash", Now.AddHours(1), Now);
        token.MarkUsed(Now.AddMinutes(5));

        var act = () => token.MarkUsed(Now.AddMinutes(10));

        act.Should().Throw<InvalidOperationException>();
    }
}
