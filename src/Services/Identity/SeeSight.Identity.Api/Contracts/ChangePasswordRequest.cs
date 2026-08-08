namespace SeeSight.Identity.Api.Contracts;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
