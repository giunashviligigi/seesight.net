namespace SeeSight.Identity.Api.Contracts;

public sealed record RegisterRequest(string Email, string Password, string? FirstName, string? LastName);
