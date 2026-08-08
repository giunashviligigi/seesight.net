namespace SeeSight.Identity.Api.Contracts;

/// <summary>
/// <see cref="RefreshToken"/> is optional in the body — the browser client relies
/// on the httpOnly cookie instead (docs/Authentication.md §3); non-browser
/// clients (Swagger, scripts) supply it explicitly here.
/// </summary>
public sealed record RefreshRequest(string? RefreshToken);
