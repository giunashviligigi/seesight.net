using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using SeeSight.Tenant.Application.Exceptions;

namespace SeeSight.Tenant.Api.Middleware;

/// <summary>
/// Translates Application-layer exceptions into RFC 7807 problem responses —
/// one place, not a try/catch per controller action (mirrors Identity.Api's
/// ExceptionHandlingMiddleware).
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
        catch (CompanyIdRequiredException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Bad Request", ex.Message).ConfigureAwait(false);
        }
        catch (NoCompanyAssignedException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden, "Forbidden", ex.Message).ConfigureAwait(false);
        }
        catch (CrossTenantAccessException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden, "Forbidden", ex.Message).ConfigureAwait(false);
        }
        catch (InsufficientRoleException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden, "Forbidden", ex.Message).ConfigureAwait(false);
        }
        catch (CompanyAlreadyAssignedException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status409Conflict, "Conflict", ex.Message).ConfigureAwait(false);
        }
        catch (CompanyNotFoundException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status404NotFound, "Not Found", ex.Message).ConfigureAwait(false);
        }
        catch (DepartmentNotFoundException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status404NotFound, "Not Found", ex.Message).ConfigureAwait(false);
        }
        catch (EmployeeNotFoundException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status404NotFound, "Not Found", ex.Message).ConfigureAwait(false);
        }
        catch (DuplicateDepartmentNameException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status409Conflict, "Conflict", ex.Message).ConfigureAwait(false);
        }
        catch (DuplicateEmployeeEmailException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status409Conflict, "Conflict", ex.Message).ConfigureAwait(false);
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
