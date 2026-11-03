using FluentValidation;
using Scholar.Domain.Identity;

namespace Scholar.Application.Identity;

public sealed class AuthOptions
{
    public const string Section = "Auth";

    /// <summary>HMAC key for access tokens (≥ 32 bytes). Supplied by secret store / user-secrets, never committed.</summary>
    public string JwtSigningKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "scholar-suite";
    public string Audience { get; set; } = "scholar-suite";
    public int AccessTokenMinutes { get; set; } = 15;
    /// <summary>Sliding lifetime of each refresh token.</summary>
    public int RefreshTokenDays { get; set; } = 14;
    /// <summary>Absolute lifetime of a session regardless of activity.</summary>
    public int SessionMaxDays { get; set; } = 30;
    /// <summary>Secure + __Host- prefixed cookies. Disabled only for plain-HTTP local development.</summary>
    public bool SecureCookies { get; set; } = true;
    /// <summary>Base URL of the web app, used in password-reset and invitation links.</summary>
    public string PublicAppUrl { get; set; } = "http://localhost:3000";
}

/// <summary>Authentication failed (bad credentials, expired or reused token). Surfaces as HTTP 401.</summary>
public sealed class UnauthorizedException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed record LoginRequest(string Identifier, string Password, string? SchoolCode = null);

public sealed record AuthTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    Guid SessionId);

public sealed record AuthUserDto(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    string? Email,
    string UserName,
    bool MustChangePassword,
    bool IsPlatformAdmin,
    Guid? SchoolId);

public sealed record AuthResult(AuthTokens Tokens, AuthUserDto User);

public sealed record ForgotPasswordRequest(string Identifier);
public sealed record ResetPasswordRequest(Guid UserId, string Token, string NewPassword);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record SwitchSchoolRequest(Guid SchoolId);

public sealed record SessionDto(
    Guid Id,
    ClientType ClientType,
    string? UserAgent,
    string? IpAddress,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset ExpiresAt,
    bool IsCurrent);

public sealed record SchoolSummaryDto(Guid Id, string Code, string Name, string? ShortName, Guid? LogoFileId, string? BrandColor);

/// <summary>Issues signed access tokens for a session.</summary>
public interface IAccessTokenIssuer
{
    (string Token, DateTimeOffset ExpiresAt) Issue(User user, UserSession session);
}

/// <summary>Sends account e-mails (reset links, invitations). Implemented by the communication module.</summary>
public interface IAccountNotifier
{
    Task SendPasswordResetAsync(User user, string resetLink, CancellationToken ct);
    Task SendInvitationAsync(User user, string schoolName, string setupLink, CancellationToken ct);
}

/// <summary>Cached session state consulted on every authenticated request.</summary>
public interface ISessionValidator
{
    Task<SessionState?> GetAsync(Guid sessionId, CancellationToken ct);
    void Invalidate(Guid sessionId);
}

public sealed record SessionState(Guid SessionId, Guid UserId, bool IsActive, bool MustChangePassword);

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Identifier).NotEmpty().MaximumLength(254).WithName("Email or username");
        RuleFor(x => x.Password).NotEmpty().MaximumLength(256);
        RuleFor(x => x.SchoolCode).MaximumLength(32);
    }
}

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator() => RuleFor(x => x.Identifier).NotEmpty().MaximumLength(254);
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Token).NotEmpty().MaximumLength(2048);
        RuleFor(x => x.NewPassword).ApplyPasswordPolicy();
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).ApplyPasswordPolicy()
            .NotEqual(x => x.CurrentPassword).WithMessage("Choose a password different from the current one.");
    }
}

public static class PasswordPolicy
{
    public const int MinimumLength = 8;

    public static IRuleBuilderOptions<T, string> ApplyPasswordPolicy<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MinimumLength(MinimumLength).WithMessage($"Use at least {MinimumLength} characters.")
            .MaximumLength(128)
            .Must(p => p.Any(char.IsLetter) && p.Any(char.IsDigit))
            .WithMessage("Use a mix of letters and numbers.");
}
