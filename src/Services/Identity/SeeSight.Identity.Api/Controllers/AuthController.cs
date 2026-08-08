using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SeeSight.Identity.Api.Contracts;
using SeeSight.Identity.Application.Users;
using SeeSight.SharedKernel.Http;

namespace SeeSight.Identity.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController(
    ISender sender,
    ICurrentUserContext currentUser,
    IWebHostEnvironment environment,
    IConfiguration configuration) : ControllerBase
{
    /// <summary>
    /// Self-signup — always creates a CompanyAdmin with no company assigned yet,
    /// and logs the user in immediately (same response shape as login — the
    /// Gateway sets both session cookies from this response either way).
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var command = new RegisterUserCommand(request.Email, request.Password, request.FirstName, request.LastName, ClientIp);
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(Me), null, AuthResponse.FromResult(result));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var command = new LoginCommand(request.Email, request.Password, ClientIp);
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(AuthResponse.FromResult(result));
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

    /// <summary>
    /// The refresh token is read from the cookie the Gateway forwards unchanged
    /// (see docs/Authentication.md §3) if the request body doesn't supply one
    /// explicitly — the same dual-delivery pattern as the access token, for
    /// non-browser clients.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest? request, CancellationToken cancellationToken)
    {
        var token = request?.RefreshToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            Request.Cookies.TryGetValue(AuthCookieNames.RefreshToken, out token);
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized();
        }

        var result = await sender.Send(new RefreshTokenCommand(token, ClientIp), cancellationToken).ConfigureAwait(false);
        return Ok(AuthResponse.FromResult(result));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutRequest? request, CancellationToken cancellationToken)
    {
        var token = request?.RefreshToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            Request.Cookies.TryGetValue(AuthCookieNames.RefreshToken, out token);
        }

        await sender.Send(new LogoutCommand(token), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("forgot-password")]
    public async Task<ActionResult<ForgotPasswordResponse>> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ForgotPasswordCommand(request.Email), cancellationToken).ConfigureAwait(false);

        const string genericMessage = "If an account with that email exists, a password reset link has been sent.";

        // Debug fields exposed only in Development, and only when a token was
        // actually issued (i.e. a matching user exists) — never in
        // Staging/Production, per docs/Authentication.md §4.
        if (!environment.IsDevelopment() || result.DebugToken is null)
        {
            return Ok(new ForgotPasswordResponse(genericMessage, null, null));
        }

        var frontendBaseUrl = configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
        var resetUrl = $"{frontendBaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(result.DebugToken)}";

        return Ok(new ForgotPasswordResponse(genericMessage, result.DebugToken, resetUrl));
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await sender.Send(new ResetPasswordCommand(request.Token, request.NewPassword), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        await sender.Send(new ChangePasswordCommand(userId, request.CurrentPassword, request.NewPassword), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();
}
