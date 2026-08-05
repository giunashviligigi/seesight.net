namespace SeeSight.Identity.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Identity:Jwt";

    /// <summary>
    /// PEM-encoded RSA private key (PKCS#8). Required outside Development —
    /// validated via <see cref="JwtOptionsValidator"/> + ValidateOnStart(), per
    /// docs/Authentication.md §5. In Development, if unset, an ephemeral key is
    /// generated at startup (tokens won't survive a restart).
    /// </summary>
    public string? SigningKeyPem { get; set; }

    public string Issuer { get; set; } = "https://seesight.identity";

    public string Audience { get; set; } = "seesight";

    public int AccessTokenLifetimeMinutes { get; set; } = 15;
}
