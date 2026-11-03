using Microsoft.Extensions.Options;
using Scholar.Api.Auth;
using Scholar.Application.Access;
using Scholar.Application.Identity;
using Scholar.Domain.Identity;

namespace Scholar.Api.Endpoints;

public sealed record CsrfResponse(string Token);
public sealed record BrowserSessionResponse(AuthUserDto User, DateTimeOffset AccessTokenExpiresAt);
public sealed record TokenResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt, AuthUserDto User);
public sealed record MobileRefreshRequest(string RefreshToken);

internal static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder api)
    {
        var auth = api.MapGroup("/auth").WithTags("Authentication");

        auth.MapGet("/csrf", (HttpContext http, IOptions<AuthOptions> options) =>
                new CsrfResponse(AuthCookies.EnsureCsrf(http, options.Value)))
            .AllowAnonymous()
            .WithSummary("Issue the CSRF cookie a browser needs before signing in.");

        auth.MapGet("/options", (AccountOnboarding onboarding) => new SignInOptionsResponse(onboarding.CanEmail))
            .AllowAnonymous()
            .WithSummary("What the sign-in pages can offer (e.g. whether reset links can be e-mailed). Says nothing about any account.");

        auth.MapPost("/login", async (LoginRequest request, AuthService service, HttpContext http,
                IOptions<AuthOptions> options, CancellationToken ct) =>
            {
                var result = await service.LoginAsync(request, ClientType.Browser, ct);
                AuthCookies.Write(http, result.Tokens, options.Value);
                return new BrowserSessionResponse(result.User, result.Tokens.AccessTokenExpiresAt);
            })
            .AllowAnonymous()
            .RequireRateLimiting(AuthSetup.AuthRateLimit)
            .WithSummary("Browser sign-in. Sets HttpOnly session cookies.");

        auth.MapPost("/refresh", async (AuthService service, HttpContext http, IOptions<AuthOptions> options,
                CancellationToken ct) =>
            {
                http.Request.Cookies.TryGetValue(AuthCookies.RefreshName(options.Value), out var refreshToken);
                try
                {
                    var result = await service.RefreshAsync(refreshToken ?? string.Empty, ct);
                    AuthCookies.Write(http, result.Tokens, options.Value);
                    return new BrowserSessionResponse(result.User, result.Tokens.AccessTokenExpiresAt);
                }
                catch (UnauthorizedException ex) when (ex.Code != "auth.refresh_in_progress")
                {
                    AuthCookies.Clear(http, options.Value);
                    throw;
                }
            })
            .AllowAnonymous()
            .WithSummary("Rotate the browser session's refresh token and issue a new access token.");

        auth.MapPost("/logout", async (AuthService service, HttpContext http, IOptions<AuthOptions> options,
                CancellationToken ct) =>
            {
                if (http.User.Identity?.IsAuthenticated == true)
                {
                    await service.LogoutAsync(ct);
                }
                else if (http.Request.Cookies.TryGetValue(AuthCookies.RefreshName(options.Value), out var refreshToken))
                {
                    await service.LogoutWithRefreshTokenAsync(refreshToken, ct);
                }

                AuthCookies.Clear(http, options.Value);
                return Results.NoContent();
            })
            .AllowAnonymous()
            .WithSummary("End the current session and clear cookies.");

        auth.MapPost("/token", async (LoginRequest request, AuthService service, CancellationToken ct) =>
                ToTokenResponse(await service.LoginAsync(request, ClientType.Mobile, ct)))
            .AllowAnonymous()
            .RequireRateLimiting(AuthSetup.AuthRateLimit)
            .WithSummary("Native-client sign-in. Returns tokens in the body.");

        auth.MapPost("/token/refresh", async (MobileRefreshRequest request, AuthService service, CancellationToken ct) =>
                ToTokenResponse(await service.RefreshAsync(request.RefreshToken, ct)))
            .AllowAnonymous()
            .WithSummary("Native-client token rotation.");

        auth.MapPost("/switch-school", async (SwitchSchoolRequest request, AuthService service, HttpContext http,
                IOptions<AuthOptions> options, CancellationToken ct) =>
            {
                var result = await service.SwitchSchoolAsync(request.SchoolId, ct);
                if (http.Items.ContainsKey(AuthCookies.ViaCookieItem))
                {
                    AuthCookies.Write(http, result.Tokens, options.Value);
                    return Results.Ok(new BrowserSessionResponse(result.User, result.Tokens.AccessTokenExpiresAt));
                }

                return Results.Ok(ToTokenResponse(result));
            })
            .WithSummary("Change the school the session works in.");

        auth.MapPost("/password/forgot", async (ForgotPasswordRequest request, AuthService service, CancellationToken ct) =>
            {
                await service.ForgotPasswordAsync(request, ct);
                return Results.Accepted();
            })
            .AllowAnonymous()
            .RequireRateLimiting(AuthSetup.AuthRateLimit)
            .WithSummary("Send a password reset link if the account exists (always 202).");

        auth.MapPost("/password/reset", async (ResetPasswordRequest request, AuthService service, CancellationToken ct) =>
            {
                await service.ResetPasswordAsync(request, ct);
                return Results.NoContent();
            })
            .AllowAnonymous()
            .RequireRateLimiting(AuthSetup.AuthRateLimit)
            .WithSummary("Set a new password using a reset token. Ends all sessions.");

        auth.MapPost("/password/change", async (ChangePasswordRequest request, AuthService service, CancellationToken ct) =>
            {
                await service.ChangePasswordAsync(request, ct);
                return Results.NoContent();
            })
            .WithSummary("Change the signed-in user's password. Ends other sessions.");

        auth.MapGet("/me", (MeService service, CancellationToken ct) => service.GetAsync(ct))
            .WithSummary("The signed-in user, active school, permissions, campus scope and portals.");

        auth.MapGet("/sessions", (AuthService service, CancellationToken ct) => service.ListSessionsAsync(ct))
            .WithSummary("Active sessions (devices) of the signed-in user.");

        auth.MapDelete("/sessions/{id:guid}", async (Guid id, AuthService service, CancellationToken ct) =>
            {
                await service.RevokeSessionAsync(id, ct);
                return Results.NoContent();
            })
            .WithSummary("Sign out a device.");
    }

    private static TokenResponse ToTokenResponse(AuthResult result) =>
        new(result.Tokens.AccessToken, result.Tokens.AccessTokenExpiresAt, result.Tokens.RefreshToken,
            result.Tokens.RefreshTokenExpiresAt, result.User);
}

/// <param name="PasswordResetByEmail">False while no e-mail provider is configured, so the page doesn't promise a link.</param>
public sealed record SignInOptionsResponse(bool PasswordResetByEmail);
