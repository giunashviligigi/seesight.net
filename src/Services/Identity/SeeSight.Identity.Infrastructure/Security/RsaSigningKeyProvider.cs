using System.Security.Cryptography;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace SeeSight.Identity.Infrastructure.Security;

/// <summary>
/// Holds the RSA key pair Identity Service signs access tokens with, and exposes
/// it as a JWKS document for the Gateway (and anyone else) to validate signatures
/// against — see docs/Authentication.md §2, §8.
/// </summary>
public sealed class RsaSigningKeyProvider
{
    public RSA Rsa { get; }

    public string KeyId { get; }

    public RsaSigningKeyProvider(
        IOptions<JwtOptions> options,
        IHostEnvironment environment,
        ILogger<RsaSigningKeyProvider> logger)
    {
        var pem = options.Value.SigningKeyPem;

        if (string.IsNullOrWhiteSpace(pem))
        {
            // JwtOptionsValidator already guarantees we only get here in Development.
            RsaSigningKeyProviderLog.EphemeralKeyGenerated(logger);
            Rsa = RSA.Create(2048);
        }
        else
        {
            Rsa = RSA.Create();
            Rsa.ImportFromPem(pem);
        }

        KeyId = ComputeKeyId(Rsa);
    }

    /// <summary>
    /// Public key only — <see cref="RSA.ExportParameters"/>(includePrivateParameters:
    /// false) is essential here. <see cref="JsonWebKeyConverter.ConvertFromRSASecurityKey"/>
    /// happily serializes whatever key material the RSA instance holds; passing
    /// <see cref="Rsa"/> (which holds the private key) directly would leak d/p/q/dp/dq/qi
    /// — the private key itself — to every caller of the public JWKS endpoint.
    /// </summary>
    public JsonWebKeySet GetJsonWebKeySet()
    {
        var publicParameters = Rsa.ExportParameters(includePrivateParameters: false);
        using var publicOnlyRsa = RSA.Create(publicParameters);

        var securityKey = new RsaSecurityKey(publicOnlyRsa) { KeyId = KeyId };
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(securityKey);
        jwk.Use = "sig";
        jwk.Alg = SecurityAlgorithms.RsaSha256;

        var jwks = new JsonWebKeySet();
        jwks.Keys.Add(jwk);
        return jwks;
    }

    private static string ComputeKeyId(RSA rsa)
    {
        var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
        var hash = SHA256.HashData(publicKeyBytes);
        return Convert.ToBase64String(hash)[..16]
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
