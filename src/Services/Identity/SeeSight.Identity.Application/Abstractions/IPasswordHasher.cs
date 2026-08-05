namespace SeeSight.Identity.Application.Abstractions;

/// <summary>Implemented in Infrastructure via BCrypt — see docs/Authentication.md §4.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
