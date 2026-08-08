using System.Security.Cryptography;
using SeeSight.Identity.Application.Abstractions;

namespace SeeSight.Identity.Infrastructure.Security;

/// <summary>32 random bytes (256 bits), base64url-encoded — matches the original system's reset-token generation (docs/Authentication.md §4).</summary>
public sealed class SecureOpaqueTokenGenerator : IOpaqueTokenGenerator
{
    private const int TokenSizeBytes = 32;

    public string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenSizeBytes);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
