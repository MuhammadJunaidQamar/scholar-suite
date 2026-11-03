using System.Security.Cryptography;
using System.Text;
using Scholar.Application.Identity;

namespace Scholar.Api.Auth;

/// <summary>
/// Browser token transport. Tokens never reach JavaScript: access and refresh tokens are HttpOnly cookies, and only
/// the CSRF token (which is useless without the HttpOnly cookies) is readable by the page.
/// </summary>
internal static class AuthCookies
{
    public const string CsrfHeader = "X-CSRF-Token";
    public const string ViaCookieItem = "auth.via_cookie";

    private const string AccessBase = "ss_at";
    private const string RefreshBase = "ss_rt";
    private const string CsrfBase = "ss_csrf";

    public static string AccessName(AuthOptions o) => Prefixed(o, AccessBase);
    public static string RefreshName(AuthOptions o) => Prefixed(o, RefreshBase);
    public static string CsrfName(AuthOptions o) => Prefixed(o, CsrfBase);

    public static void Write(HttpContext http, AuthTokens tokens, AuthOptions o)
    {
        http.Response.Cookies.Append(AccessName(o), tokens.AccessToken, Options(o, "/api", httpOnly: true,
            SameSiteMode.Lax, tokens.AccessTokenExpiresAt));
        http.Response.Cookies.Append(RefreshName(o), tokens.RefreshToken, Options(o, "/api/auth", httpOnly: true,
            SameSiteMode.Strict, tokens.RefreshTokenExpiresAt));
        http.Response.Cookies.Append(CsrfName(o), CsrfTokenForSession(tokens.SessionId, o), Options(o, "/", httpOnly: false,
            SameSiteMode.Strict, tokens.RefreshTokenExpiresAt));
    }

    public static void Clear(HttpContext http, AuthOptions o)
    {
        http.Response.Cookies.Delete(AccessName(o), Options(o, "/api", true, SameSiteMode.Lax, null));
        http.Response.Cookies.Delete(RefreshName(o), Options(o, "/api/auth", true, SameSiteMode.Strict, null));
        http.Response.Cookies.Delete(CsrfName(o), Options(o, "/", false, SameSiteMode.Strict, null));
    }

    /// <summary>Ensures an anonymous visitor has a CSRF cookie (needed before signing in) and returns its value.</summary>
    public static string EnsureCsrf(HttpContext http, AuthOptions o)
    {
        if (http.Request.Cookies.TryGetValue(CsrfName(o), out var existing) && existing.Length >= 32)
        {
            return existing;
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        http.Response.Cookies.Append(CsrfName(o), token, Options(o, "/", httpOnly: false, SameSiteMode.Strict, null));
        return token;
    }

    /// <summary>Session-bound CSRF value: HMAC of the session id, so a planted cookie cannot satisfy the check.</summary>
    public static string CsrfTokenForSession(Guid sessionId, AuthOptions o)
    {
        var key = SHA256.HashData(Encoding.UTF8.GetBytes("csrf:" + o.JwtSigningKey));
        var mac = HMACSHA256.HashData(key, sessionId.ToByteArray());
        return Convert.ToBase64String(mac).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string Prefixed(AuthOptions o, string name) => o.SecureCookies ? "__Secure-" + name : name;

    private static CookieOptions Options(AuthOptions o, string path, bool httpOnly, SameSiteMode sameSite, DateTimeOffset? expires) =>
        new()
        {
            HttpOnly = httpOnly,
            Secure = o.SecureCookies,
            SameSite = sameSite,
            Path = path,
            Expires = expires,
            IsEssential = true,
        };
}
