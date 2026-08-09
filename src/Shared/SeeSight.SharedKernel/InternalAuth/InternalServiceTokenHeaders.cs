namespace SeeSight.SharedKernel.InternalAuth;

/// <summary>
/// Header carrying the shared internal-service credential — see
/// docs/adr/0006-internal-service-to-service-authentication.md.
/// </summary>
public static class InternalServiceTokenHeaders
{
    public const string ServiceToken = "X-Internal-Service-Token";
}
