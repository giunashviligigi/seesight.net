using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using SeeSight.Identity.Application.Exceptions;

namespace SeeSight.Identity.Api.Middleware;

/// <summary>
/// Translates Application-layer exceptions into RFC 7807 problem responses —
/// one place, not a try/catch per controller action.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch (ValidationException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Validation failed",
                string.Join(" ", ex.Errors.Select(e => e.ErrorMessage))).ConfigureAwait(false);
        }
        catch (EmailAlreadyInUseException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status409Conflict, "Conflict", ex.Message).ConfigureAwait(false);
        }
        catch (InvalidCredentialsException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "Unauthorized", ex.Message).ConfigureAwait(false);
        }
        catch (UserSessionInvalidException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "Unauthorized", ex.Message).ConfigureAwait(false);
        }
        catch (InvalidRefreshTokenException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "Unauthorized", ex.Message).ConfigureAwait(false);
        }
        catch (RefreshTokenReuseDetectedException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "Unauthorized", ex.Message).ConfigureAwait(false);
        }
        catch (InvalidPasswordResetTokenException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Bad Request", ex.Message).ConfigureAwait(false);
        }
        catch (CurrentPasswordIncorrectException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Bad Request", ex.Message).ConfigureAwait(false);
        }
        catch (SamePasswordException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Bad Request", ex.Message).ConfigureAwait(false);
        }
        catch (UserNotFoundException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status404NotFound, "Not Found", ex.Message).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ExceptionHandlingMiddlewareLog.UnhandledException(logger, ex);
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "Internal Server Error",
                "An unexpected error occurred.").ConfigureAwait(false);
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string title, string detail)
    {
        context.Response.StatusCode = statusCode;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
        };

        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json").ConfigureAwait(false);
    }
}

internal static partial class ExceptionHandlingMiddlewareLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception while processing request")]
    public static partial void UnhandledException(ILogger logger, Exception exception);
}
