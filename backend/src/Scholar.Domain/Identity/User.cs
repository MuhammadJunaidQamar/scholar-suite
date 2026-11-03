using Microsoft.AspNetCore.Identity;
using Scholar.Domain.Common;

namespace Scholar.Domain.Identity;

public enum UserStatus { Active, Invited, Disabled }

/// <summary>
/// A person's login. Users are global to the platform; <see cref="SchoolMembership"/> links them to schools.
/// Password hashing, lockout and security stamps are handled by ASP.NET Core Identity.
/// </summary>
public sealed class User : IdentityUser<Guid>
{
    private User() { }

    public User(string userName, string firstName, string lastName, string? email = null)
    {
        Id = Guid.CreateVersion7();
        UserName = userName.Trim().ToLowerInvariant();
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        SecurityStamp = Guid.NewGuid().ToString("N");
        LockoutEnabled = true;
    }

    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public Guid? AvatarFileId { get; set; }
    public bool IsPlatformAdmin { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
    /// <summary>Provisioned accounts must choose their own password on first sign-in.</summary>
    public bool MustChangePassword { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public Guid? LastSchoolId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<SchoolMembership> Memberships { get; } = new List<SchoolMembership>();

    public string FullName => $"{FirstName} {LastName}".Trim();
}

public enum MembershipStatus { Active, Suspended }

public sealed class SchoolMembership : TenantEntity
{
    private SchoolMembership() { }

    public SchoolMembership(Guid schoolId, Guid userId)
    {
        SchoolId = schoolId;
        UserId = userId;
    }

    public Guid UserId { get; private set; }
    public User User { get; private set; } = default!;
    public MembershipStatus Status { get; set; } = MembershipStatus.Active;
    public DateTimeOffset? LastActiveAt { get; set; }
}

public enum ClientType { Browser, Mobile }

/// <summary>A signed-in device. Refresh tokens rotate within a session; revoking the session ends all of them.</summary>
public sealed class UserSession : Entity
{
    private UserSession() { }

    public UserSession(Guid userId, Guid? schoolId, ClientType clientType, DateTimeOffset now, TimeSpan absoluteLifetime,
        string? ipAddress, string? userAgent)
    {
        UserId = userId;
        SchoolId = schoolId;
        ClientType = clientType;
        CreatedAt = now;
        LastSeenAt = now;
        ExpiresAt = now.Add(absoluteLifetime);
        IpAddress = ipAddress;
        UserAgent = userAgent is { Length: > 512 } ? userAgent[..512] : userAgent;
    }

    public Guid UserId { get; private set; }
    public User User { get; private set; } = default!;
    /// <summary>The school this session is currently working in (null for platform-only users).</summary>
    public Guid? SchoolId { get; set; }
    public ClientType ClientType { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? RevokedReason { get; private set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(string reason, DateTimeOffset now)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = now;
        RevokedReason = reason;
    }
}

public sealed class RefreshToken : Entity
{
    private RefreshToken() { }

    public RefreshToken(Guid sessionId, string tokenHash, DateTimeOffset now, DateTimeOffset expiresAt)
    {
        SessionId = sessionId;
        TokenHash = tokenHash;
        CreatedAt = now;
        ExpiresAt = expiresAt;
    }

    public Guid SessionId { get; private set; }
    public UserSession Session { get; private set; } = default!;
    public string TokenHash { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public Guid? ReplacedById { get; private set; }

    public void Consume(Guid replacedById, DateTimeOffset now)
    {
        ConsumedAt = now;
        ReplacedById = replacedById;
    }
}
