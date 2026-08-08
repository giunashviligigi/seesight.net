namespace SeeSight.Identity.Application.Abstractions;

/// <summary>
/// SHA-256 hashing for opaque tokens (refresh tokens, password reset tokens) —
/// only the hash is ever persisted, per docs/Authentication.md §2/§4. Used both
/// to hash a newly-generated token before storing it, and to hash a
/// client-presented token before looking it up by hash.
/// </summary>
public interface ITokenHasher
{
    string Hash(string value);
}
