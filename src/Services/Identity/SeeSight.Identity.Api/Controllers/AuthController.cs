using MediatR;
using Microsoft.AspNetCore.Mvc;
using SeeSight.Identity.Api.Contracts;
using SeeSight.Identity.Application.Users;
using SeeSight.SharedKernel.Http;

namespace SeeSight.Identity.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController(ISender sender, ICurrentUserContext currentUser) : ControllerBase
{
    /// <summary>
    /// Self-signup — always creates a CompanyAdmin with no company assigned yet,
    /// and logs the user in immediately (same response shape as login — the
    /// Gateway sets the session cookie from this response either way).
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<LoginResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var command = new RegisterUserCommand(request.Email, request.Password, request.FirstName, request.LastName);
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(Me), null, new LoginResponse(result.AccessToken, result.ExpiresAt, result.User));
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var command = new LoginCommand(request.Email, request.Password);
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(new LoginResponse(result.AccessToken, result.ExpiresAt, result.User));
    }

    /// <summary>
    /// Relies on <see cref="ICurrentUserContext"/> (populated from the Gateway-forwarded
    /// identity headers) rather than ASP.NET Core [Authorize] — JWT validation happens
    /// once, at the Gateway, per docs/Authentication.md §8/docs/Authorization.md §2.
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await sender.Send(new GetCurrentUserQuery(userId), cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }
}
