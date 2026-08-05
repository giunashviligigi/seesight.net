namespace SeeSight.Gateway.Authentication;

public sealed class IdentityServiceOptions
{
    public const string SectionName = "IdentityService";

    public string BaseUrl { get; set; } = "http://localhost:5075";

    public int JwksRefreshIntervalMinutes { get; set; } = 5;
}
