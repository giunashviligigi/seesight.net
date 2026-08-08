using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SeeSight.Identity.Application.Abstractions;
using SeeSight.Identity.Domain;
using SeeSight.SharedKernel.Http;

namespace SeeSight.Identity.Infrastructure.Security;

public sealed class RsaJwtIssuer(RsaSigningKeyProvider keyProvider, IOptions<JwtOptions> options) : IJwtIssuer
{
    public AccessToken IssueAccessToken(User user)
    {
        var jwtOptions = options.Value;
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(jwtOptions.AccessTokenLifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            // Lets the Gateway enforce the MustChangePassword gate (docs/Authentication.md
            // §4) without a database round-trip — consistent with the rest of the
            // stateless-JWT-validation design.
            new(SeeSightClaimTypes.MustChangePassword, user.MustChangePassword ? "true" : "false"),
        };

        if (user.CompanyId is { } companyId)
        {
            claims.Add(new Claim(SeeSightClaimTypes.CompanyId, companyId.ToString()));
        }

        var signingCredentials = new SigningCredentials(
            new RsaSecurityKey(keyProvider.Rsa) { KeyId = keyProvider.KeyId },
            SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            jwtOptions.Issuer,
            jwtOptions.Audience,
            claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: signingCredentials);

        var tokenValue = new JwtSecurityTokenHandler().WriteToken(token);
        return new AccessToken(tokenValue, expiresAt);
    }

    public DateTimeOffset ComputeRefreshTokenExpiry(DateTimeOffset now) =>
        now.AddDays(options.Value.RefreshTokenLifetimeDays);
}
