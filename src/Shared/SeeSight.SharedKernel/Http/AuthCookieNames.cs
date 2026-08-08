namespace SeeSight.SharedKernel.Http;

/// <summary>
/// Default session cookie names — the Gateway writes them (login/register/refresh
/// responses) and Identity Service reads them directly for the one class of
/// endpoint that's inherently pre- or re-authentication (refresh, logout), where
/// the usual Gateway-forwarded-header pattern doesn't apply (there may be no
/// valid access token yet). Defined once so neither side can drift on the
/// literal string; each side may still override via configuration if ever
/// needed (docs/Authentication.md §3).
/// </summary>
public static class AuthCookieNames
{
    public const string AccessToken = "seesight_access_token";
    public const string RefreshToken = "seesight_refresh_token";
}
