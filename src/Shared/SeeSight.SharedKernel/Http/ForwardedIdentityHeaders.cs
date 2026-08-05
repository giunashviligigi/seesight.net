namespace SeeSight.SharedKernel.Http;

/// <summary>
/// Header names the Gateway stamps on every proxied request after validating the
/// caller's JWT, and that every downstream service reads to know who's calling —
/// see docs/Authorization.md §2. Defined once so the writer (Gateway) and every
/// reader (ICurrentUserContext) can't drift out of sync on the literal string.
/// </summary>
public static class ForwardedIdentityHeaders
{
    public const string UserId = "X-User-Id";
    public const string UserRole = "X-User-Role";
    public const string CompanyId = "X-User-Company-Id";
}
