using SeeSight.Identity.Application.Users;

namespace SeeSight.Identity.Api.Contracts;

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, UserDto User);
