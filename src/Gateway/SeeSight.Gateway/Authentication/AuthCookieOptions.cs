namespace SeeSight.Gateway.Authentication;

public sealed class AuthCookieOptions
{
    public const string SectionName = "Gateway:AuthCookie";

    public string Name { get; set; } = "seesight_access_token";
}
