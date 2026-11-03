using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scholar.Domain.Access;
using Scholar.Domain.Identity;

namespace Scholar.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.Property(u => u.FirstName).HasMaxLength(100);
        b.Property(u => u.LastName).HasMaxLength(100);
        b.Property(u => u.PhoneNumber).HasMaxLength(40);
        b.Ignore(u => u.FullName);
        // Identity creates a non-unique email index; emails must be unique platform-wide when present.
        b.HasIndex(u => u.NormalizedEmail).IsUnique().HasFilter("normalized_email IS NOT NULL")
            .HasDatabaseName("ux_users_normalized_email");
        b.HasMany(u => u.Memberships).WithOne(m => m.User).HasForeignKey(m => m.UserId);
    }
}

internal sealed class IdentityTablesConfiguration :
    IEntityTypeConfiguration<IdentityUserClaim<Guid>>,
    IEntityTypeConfiguration<IdentityUserLogin<Guid>>,
    IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> b) => b.ToTable("user_claims");
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> b) => b.ToTable("user_logins");
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> b) => b.ToTable("user_tokens");
}

internal sealed class SchoolMembershipConfiguration : IEntityTypeConfiguration<SchoolMembership>
{
    public void Configure(EntityTypeBuilder<SchoolMembership> b)
    {
        b.ToTable("school_memberships");
        b.HasIndex(m => new { m.UserId, m.SchoolId }).IsUnique();
    }
}

internal sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> b)
    {
        b.ToTable("user_sessions");
        b.Property(s => s.IpAddress).HasMaxLength(64);
        b.Property(s => s.UserAgent).HasMaxLength(512);
        b.Property(s => s.RevokedReason).HasMaxLength(80);
        b.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId);
        b.HasIndex(s => new { s.UserId, s.RevokedAt });
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens");
        b.Property(t => t.TokenHash).HasMaxLength(64);
        b.HasIndex(t => t.TokenHash).IsUnique();
        b.HasOne(t => t.Session).WithMany().HasForeignKey(t => t.SessionId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(t => t.SessionId);
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("roles");
        b.Property(r => r.Key).HasMaxLength(60);
        b.Property(r => r.Name).HasMaxLength(100);
        b.Property(r => r.Description).HasMaxLength(500);
        b.HasIndex(r => new { r.SchoolId, r.Key }).IsUnique().HasFilter("key IS NOT NULL");
        b.HasIndex(r => new { r.SchoolId, r.Name }).IsUnique();
        b.HasMany(r => r.Permissions).WithOne().HasForeignKey(p => p.RoleId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(r => r.Permissions).AutoInclude(false);
    }
}

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.ToTable("role_permissions");
        b.HasKey(p => new { p.RoleId, p.Permission });
        b.Property(p => p.Permission).HasMaxLength(80);
    }
}

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> b)
    {
        b.ToTable("user_roles");
        b.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId);
        b.HasOne(r => r.Role).WithMany().HasForeignKey(r => r.RoleId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Domain.Platform.Campus>().WithMany().HasForeignKey(r => r.CampusId);
        b.HasIndex(r => new { r.UserId, r.RoleId, r.CampusId }).IsUnique().AreNullsDistinct(false);
        b.HasIndex(r => new { r.SchoolId, r.UserId });
    }
}
