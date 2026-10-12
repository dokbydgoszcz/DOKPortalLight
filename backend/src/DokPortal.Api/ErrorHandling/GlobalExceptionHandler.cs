using DokPortal.Application.People;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.ErrorHandling;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            _ when IsTransientDatabaseFailure(exception) => (StatusCodes.Status503ServiceUnavailable,
                "Baza danych chwilowo nie odpowiada (wybudza się po przerwie). Spróbuj ponownie za minutę."),
            DuplicatePersonException => (StatusCodes.Status409Conflict, exception.Message),
            InvalidOperationException => (StatusCodes.Status400BadRequest, exception.Message),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Brak uprawnień do wykonania tej operacji."),
            _ => (StatusCodes.Status500InternalServerError, "Wystąpił nieoczekiwany błąd serwera.")
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception while processing {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            _logger.LogWarning(exception, "Handled exception while processing {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }

        if (statusCode == StatusCodes.Status503ServiceUnavailable)
        {
            httpContext.Response.Headers.RetryAfter = "30";
        }
        httpContext.Response.StatusCode = statusCode;
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title
        };
        if (exception is DuplicatePersonException duplicate)
        {
            problem.Extensions["code"] = duplicate.Code;
            problem.Extensions["duplicates"] = duplicate.Matches;
        }
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }

    /// <summary>
    /// Przejściowy błąd bazy (budzenie darmowej bazy Azure SQL, limit czasu, wyczerpane ponowienia) – nie wina klienta.
    /// EF zgłasza go jako InvalidOperationException z wewnętrznym błędem SQL/limitu czasu, więc sprawdzamy cały łańcuch.
    /// </summary>
    private static bool IsTransientDatabaseFailure(Exception exception)
    {
        for (var e = exception; e is not null; e = e.InnerException)
        {
            if (e is Microsoft.Data.SqlClient.SqlException or TimeoutException or Microsoft.EntityFrameworkCore.Storage.RetryLimitExceededException)
            {
                return true;
            }
            if (e is InvalidOperationException && e.Message.Contains("transient failure", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
