using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Scholar.Api.Infrastructure;
using Scholar.Application.Identity;
using Scholar.Domain.Identity;

namespace Scholar.Api.Auth;

internal sealed class JwtAccessTokenIssuer(IOptions<AuthOptions> options, TimeProvider clock) : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public (string Token, DateTimeOffset ExpiresAt) Issue(User user, UserSession session)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(settings.AccessTokenMinutes);
        var claims = new Dictionary<string, object>
        {
            [ScholarClaims.UserId] = user.Id.ToString(),
            [ScholarClaims.SessionId] = session.Id.ToString(),
            ["name"] = user.FullName,
        };
        if (session.SchoolId is { } schoolId)
        {
            claims[ScholarClaims.SchoolId] = schoolId.ToString();
        }

        if (user.IsPlatformAdmin)
        {
            claims[ScholarClaims.PlatformAdmin] = "1";
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            Claims = claims,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(SigningKey(settings), SecurityAlgorithms.HmacSha256),
        };
        return (_handler.CreateToken(descriptor), expires);
    }

    public static SymmetricSecurityKey SigningKey(AuthOptions settings)
    {
        var bytes = Encoding.UTF8.GetBytes(settings.JwtSigningKey);
        if (bytes.Length < 32)
        {
            throw new InvalidOperationException(
                "Auth:JwtSigningKey must be at least 32 bytes. Configure it with user-secrets or the Auth__JwtSigningKey variable.");
        }

        return new SymmetricSecurityKey(bytes);
    }
}
