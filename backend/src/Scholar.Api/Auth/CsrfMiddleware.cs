using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Scholar.Api.Infrastructure;
using Scholar.Application.Identity;

namespace Scholar.Api.Auth;

/// <summary>
/// Double-submit CSRF protection for cookie-authenticated (browser) requests. Unsafe requests must echo the CSRF
/// cookie in the <c>X-CSRF-Token</c> header; when the request is authenticated by cookie the value must also be bound
/// to the session. Bearer-token clients (mobile) are exempt because browsers cannot attach that header cross-site.
/// </summary>
internal sealed class CsrfMiddleware(RequestDelegate next, IOptions<AuthOptions> options)
{
    private static readonly PathString[] Exempt =
    [
        "/api/auth/token",       // mobile sign-in & refresh: body credentials, no cookies
        "/api/public",           // anonymous public forms (online admissions)
        "/api/webhooks",         // provider callbacks (signature-verified)
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        if (RequiresCheck(context) && !IsValid(context))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            var problem = new ProblemDetails
            {
                Status = 403,
                Title = "Request blocked",
                Detail = "The request is missing a valid security token. Reload the page and try again.",
            };
            problem.Extensions["code"] = "csrf.invalid";
            await context.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json");
            return;
        }

        await next(context);
    }

    private static bool RequiresCheck(HttpContext context)
    {
        var request = context.Request;
        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method)
            || HttpMethods.IsTrace(request.Method))
        {
            return false;
        }

        if (!request.Path.StartsWithSegments("/api") || Exempt.Any(p => request.Path.StartsWithSegments(p)))
        {
            return false;
        }

        var authorization = request.Headers.Authorization.ToString();
        return !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsValid(HttpContext context)
    {
        var settings = options.Value;
        var header = context.Request.Headers[AuthCookies.CsrfHeader].ToString();
        if (string.IsNullOrEmpty(header) || !context.Request.Cookies.TryGetValue(AuthCookies.CsrfName(settings), out var cookie)
            || !FixedEquals(header, cookie))
        {
            return false;
        }

        if (context.Items.ContainsKey(AuthCookies.ViaCookieItem) && context.User.Identity?.IsAuthenticated == true
            && Guid.TryParse(context.User.FindFirst(ScholarClaims.SessionId)?.Value, out var sessionId))
        {
            return FixedEquals(cookie, AuthCookies.CsrfTokenForSession(sessionId, settings));
        }

        return true;
    }

    private static bool FixedEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
