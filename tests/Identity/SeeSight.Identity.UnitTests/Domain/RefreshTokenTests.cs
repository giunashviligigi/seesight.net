using FluentAssertions;
using SeeSight.Identity.Domain;

namespace SeeSight.Identity.UnitTests.Domain;

public sealed class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.CreateVersion7();

    [Fact]
    public void Issue_sets_the_expected_fields()
    {
        var expiresAt = Now.AddDays(30);

        var token = RefreshToken.Issue(UserId, "token-hash", expiresAt, "203.0.113.5", Now);

        token.Id.Should().NotBe(Guid.Empty);
        token.UserId.Should().Be(UserId);
        token.TokenHash.Should().Be("token-hash");
        token.ExpiresAt.Should().Be(expiresAt);
        token.CreatedByIp.Should().Be("203.0.113.5");
        token.CreatedAt.Should().Be(Now);
        token.RevokedAt.Should().BeNull();
        token.ReplacedByTokenId.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Issue_throws_for_missing_token_hash(string? tokenHash)
    {
        var act = () => RefreshToken.Issue(UserId, tokenHash!, Now.AddDays(30), null, Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Issue_throws_when_expiry_is_not_in_the_future()
    {
        var act = () => RefreshToken.Issue(UserId, "token-hash", Now, null, Now);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void IsActive_is_true_for_a_fresh_unexpired_token()
    {
        var token = RefreshToken.Issue(UserId, "token-hash", Now.AddDays(30), null, Now);

        token.IsActive(Now).Should().BeTrue();
    }

    [Fact]
    public void IsActive_is_false_once_expired()
    {
        var token = RefreshToken.Issue(UserId, "token-hash", Now.AddDays(30), null, Now);

        token.IsActive(Now.AddDays(31)).Should().BeFalse();
    }

    [Fact]
    public void IsActive_is_false_once_revoked()
    {
        var token = RefreshToken.Issue(UserId, "token-hash", Now.AddDays(30), null, Now);

        token.Revoke(Now.AddHours(1));

        token.IsActive(Now.AddHours(2)).Should().BeFalse();
    }

    [Fact]
    public void Revoke_records_the_replacement_token_id()
    {
        var token = RefreshToken.Issue(UserId, "token-hash", Now.AddDays(30), null, Now);
        var replacementId = Guid.CreateVersion7();

        token.Revoke(Now.AddHours(1), replacementId);

        token.RevokedAt.Should().Be(Now.AddHours(1));
        token.ReplacedByTokenId.Should().Be(replacementId);
    }

    [Fact]
    public void Revoke_is_idempotent_and_keeps_the_first_revocation()
    {
        var token = RefreshToken.Issue(UserId, "token-hash", Now.AddDays(30), null, Now);
        var firstReplacementId = Guid.CreateVersion7();

        token.Revoke(Now.AddHours(1), firstReplacementId);
        token.Revoke(Now.AddHours(2), Guid.CreateVersion7());

        token.RevokedAt.Should().Be(Now.AddHours(1));
        token.ReplacedByTokenId.Should().Be(firstReplacementId);
    }
}
