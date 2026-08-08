namespace SeeSight.SharedKernel.Http;

/// <summary>
/// Custom JWT claim type names — Identity Service writes them (RsaJwtIssuer),
/// the Gateway reads them (MustChangePasswordMiddleware, ForwardedIdentityTransformProvider).
/// Defined once so both sides can't drift on the literal string.
/// </summary>
public static class SeeSightClaimTypes
{
    public const string CompanyId = "companyId";
    public const string MustChangePassword = "mustChangePassword";
}
