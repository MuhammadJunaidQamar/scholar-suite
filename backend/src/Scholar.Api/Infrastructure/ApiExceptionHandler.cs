using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Scholar.Application.Common;
using Scholar.Domain.Common;

namespace Scholar.Api.Infrastructure;

/// <summary>
/// Translates exceptions into RFC 9457 problem details with stable <c>code</c> extensions.
/// Unexpected exceptions are logged and returned as an opaque 500 (no internals leak to clients).
/// </summary>
internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = Map(exception);
        if (problem is null)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
            problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Something went wrong",
                Detail = "An unexpected error occurred. The problem has been logged.",
            };
            problem.Extensions["code"] = "server.error";
        }
        else if (problem.Status >= 500)
        {
            logger.LogError(exception, "Server error for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            problem.Extensions.TryGetValue("code", out var code);
            logger.LogInformation("Request rejected ({Status}) {Code}: {Message}", problem.Status, code, exception.Message);
        }

        httpContext.Response.StatusCode = problem.Status ?? 500;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails? Map(Exception exception) => exception switch
    {
        ValidationException validation => Validation(validation),
        NotFoundException notFound => Problem(404, "Not found", notFound.Message, "resource.not_found"),
        ForbiddenException forbidden => Problem(403, "Forbidden", forbidden.Message, "access.forbidden"),
        ConflictException conflict => Problem(409, "Conflict", conflict.Message, conflict.Code),
        DomainException domain => Problem(422, "Request could not be completed", domain.Message, domain.Code),
        DbUpdateConcurrencyException => Problem(409, "Record changed",
            "This record was changed by someone else. Reload it and try again.", "concurrency.conflict"),
        DbUpdateException { InnerException: PostgresException pg } => MapPostgres(pg),
        BadHttpRequestException bad => Problem(400, "Bad request", bad.Message, "request.invalid"),
        _ => null,
    };

    private static ProblemDetails MapPostgres(PostgresException pg) => pg.SqlState switch
    {
        PostgresErrorCodes.UniqueViolation => Problem(409, "Duplicate", UniqueMessage(pg.ConstraintName), "db.duplicate"),
        PostgresErrorCodes.ForeignKeyViolation => Problem(409, "Related record problem",
            "The record references, or is referenced by, other data and cannot be changed this way.", "db.foreign_key"),
        PostgresErrorCodes.CheckViolation => Problem(422, "Invalid value", "A value is outside the allowed range.", "db.check"),
        PostgresErrorCodes.ExclusionViolation => Problem(409, "Overlap", "This overlaps with an existing booking.", "db.overlap"),
        PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected =>
            Problem(409, "Please retry", "The operation collided with another one. Please try again.", "db.retry"),
        _ => Problem(500, "Database error", "The database rejected the operation.", "db.error"),
    };

    private static string UniqueMessage(string? constraint) => constraint switch
    {
        null => "A record with the same values already exists.",
        _ when constraint.Contains("idempotency", StringComparison.Ordinal) => "This request was already processed.",
        _ => "A record with the same values already exists.",
    };

    private static ProblemDetails Validation(ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(e => ToCamelPath(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
        var problem = new ValidationProblemDetails(errors)
        {
            Status = 400,
            Title = "Please correct the highlighted fields",
            Detail = errors.Count == 1 ? errors.First().Value.FirstOrDefault() : $"{errors.Count} fields need attention.",
        };
        problem.Extensions["code"] = "validation.failed";
        return problem;
    }

    private static string ToCamelPath(string path) =>
        string.Join('.', path.Split('.').Select(p => p.Length == 0 ? p : char.ToLowerInvariant(p[0]) + p[1..]));

    private static ProblemDetails Problem(int status, string title, string detail, string code)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        problem.Extensions["code"] = code;
        return problem;
    }
}
