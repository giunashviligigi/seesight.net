namespace SeeSight.Identity.Api.Contracts;

public sealed record ResetPasswordRequest(string Token, string NewPassword);
