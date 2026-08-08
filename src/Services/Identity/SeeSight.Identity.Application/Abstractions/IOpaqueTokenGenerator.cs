namespace SeeSight.Identity.Application.Abstractions;

/// <summary>
/// Generates a cryptographically random, URL-safe opaque token — the raw value
/// handed to the client for refresh/password-reset tokens. Implemented in
/// Infrastructure (System.Security.Cryptography).
/// </summary>
public interface IOpaqueTokenGenerator
{
    string Generate();
}
