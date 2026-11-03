using System.Security.Claims;
using Scholar.Application.Abstractions;

namespace Scholar.Api.Infrastructure;

/// <summary>Claim names carried by access tokens. Kept short; permissions are deliberately not embedded.</summary>
public static class ScholarClaims
{
    public const string UserId = "sub";
    public const string SessionId = "sid";
    public const string SchoolId = "school";
    public const string PlatformAdmin = "pa";
}

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
    public Guid? UserId => Read(ScholarClaims.UserId);
    public Guid? SessionId => Read(ScholarClaims.SessionId);
    public Guid? SchoolId => Read(ScholarClaims.SchoolId);
    public bool IsPlatformAdmin => IsAuthenticated && Principal?.FindFirstValue(ScholarClaims.PlatformAdmin) == "1";
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    public string? UserAgent => accessor.HttpContext?.Request.Headers.UserAgent.ToString();

    private Guid? Read(string claim) =>
        IsAuthenticated && Guid.TryParse(Principal?.FindFirstValue(claim), out var value) ? value : null;
}
