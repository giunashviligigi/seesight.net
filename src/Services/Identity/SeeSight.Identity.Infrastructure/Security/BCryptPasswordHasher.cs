using SeeSight.Identity.Application.Abstractions;

namespace SeeSight.Identity.Infrastructure.Security;

/// <summary>BCrypt, 12 rounds — matches the original system's work factor exactly, per docs/Authentication.md §4.</summary>
public sealed class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string passwordHash) => BCrypt.Net.BCrypt.Verify(password, passwordHash);
}
