using System.Security.Cryptography;
using System.Text;
using SeeSight.Identity.Application.Abstractions;

namespace SeeSight.Identity.Infrastructure.Security;

/// <summary>SHA-256, hex-encoded — matches the original system's reset-token hashing approach (docs/Authentication.md §4).</summary>
public sealed class Sha256TokenHasher : ITokenHasher
{
    public string Hash(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
