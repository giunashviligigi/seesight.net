using SeeSight.SharedKernel.Http;

namespace SeeSight.Gateway.Authentication;

public sealed class AuthCookieOptions
{
    public const string SectionName = "Gateway:AuthCookie";

    public string Name { get; set; } = AuthCookieNames.AccessToken;

    public string RefreshTokenName { get; set; } = AuthCookieNames.RefreshToken;
}
