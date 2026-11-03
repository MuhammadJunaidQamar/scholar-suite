using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scholar.Domain.Auditing;
using Scholar.Domain.Common;
using Scholar.Domain.Platform;

namespace Scholar.Infrastructure.Persistence.Configurations;

internal static class ConfigurationExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    /// <summary>Stores a POCO as one jsonb document with value-based change tracking.</summary>
    public static PropertyBuilder<T> HasJsonConversion<T>(this PropertyBuilder<T> property) where T : class, new()
    {
        property.HasColumnType("jsonb").HasConversion(
            v => JsonSerializer.Serialize(v, JsonOptions),
            v => JsonSerializer.Deserialize<T>(v, JsonOptions) ?? new T(),
            new ValueComparer<T>(
                (a, b) => JsonSerializer.Serialize(a, JsonOptions) == JsonSerializer.Serialize(b, JsonOptions),
                v => JsonSerializer.Serialize(v, JsonOptions).GetHashCode(StringComparison.Ordinal),
                v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, JsonOptions), JsonOptions)!));
        return property;
    }

    public static void OwnsAddress<TEntity>(this EntityTypeBuilder<TEntity> builder,
        System.Linq.Expressions.Expression<Func<TEntity, Address?>> address) where TEntity : class
    {
        builder.ComplexProperty(address, a =>
        {
            a.Property(x => x.Line1).HasMaxLength(200);
            a.Property(x => x.Line2).HasMaxLength(200);
            a.Property(x => x.City).HasMaxLength(100);
            a.Property(x => x.Province).HasMaxLength(100);
            a.Property(x => x.PostalCode).HasMaxLength(20);
            a.Property(x => x.CountryCode).HasMaxLength(2);
        });
    }
}

internal sealed class SchoolConfiguration : IEntityTypeConfiguration<School>
{
    public void Configure(EntityTypeBuilder<School> b)
    {
        b.ToTable("schools");
        b.HasIndex(s => s.Code).IsUnique();
        b.Property(s => s.Code).HasMaxLength(32);
        b.Property(s => s.Name).HasMaxLength(200);
        b.Property(s => s.ShortName).HasMaxLength(60);
        b.Property(s => s.LegalName).HasMaxLength(250);
        b.Property(s => s.Motto).HasMaxLength(200);
        b.Property(s => s.RegistrationNumber).HasMaxLength(80);
        b.Property(s => s.Email).HasMaxLength(254);
        b.Property(s => s.Phone).HasMaxLength(40);
        b.Property(s => s.Website).HasMaxLength(200);
        b.Property(s => s.Timezone).HasMaxLength(64);
        b.Property(s => s.CurrencyCode).HasMaxLength(3).IsFixedLength();
        b.Property(s => s.Locale).HasMaxLength(16);
        b.Property(s => s.BrandColor).HasMaxLength(7);
        b.OwnsAddress(s => s.Address);
        b.Property(s => s.Settings).HasJsonConversion();
        b.HasMany(s => s.Campuses).WithOne().HasForeignKey(c => c.SchoolId);
    }
}

internal sealed class CampusConfiguration : IEntityTypeConfiguration<Campus>
{
    public void Configure(EntityTypeBuilder<Campus> b)
    {
        b.ToTable("campuses");
        b.Property(c => c.Code).HasMaxLength(20);
        b.Property(c => c.Name).HasMaxLength(150);
        b.Property(c => c.Phone).HasMaxLength(40);
        b.Property(c => c.Email).HasMaxLength(254);
        b.OwnsAddress(c => c.Address);
        b.HasIndex(c => new { c.SchoolId, c.Code }).IsUnique();
        b.HasIndex(c => c.SchoolId, "ux_campuses_one_main_per_school").IsUnique().HasFilter("is_main");
    }
}

internal sealed class NumberSequenceConfiguration : IEntityTypeConfiguration<NumberSequence>
{
    public void Configure(EntityTypeBuilder<NumberSequence> b)
    {
        b.ToTable("number_sequences");
        b.Property(s => s.Key).HasMaxLength(40);
        b.Property(s => s.Prefix).HasMaxLength(20);
        b.Property(s => s.Pattern).HasMaxLength(80);
        b.HasIndex(s => new { s.SchoolId, s.Key }).IsUnique();
    }
}

internal sealed class NumberSequenceCounterConfiguration : IEntityTypeConfiguration<NumberSequenceCounter>
{
    public void Configure(EntityTypeBuilder<NumberSequenceCounter> b)
    {
        b.ToTable("number_sequence_counters", t => t.HasCheckConstraint("ck_number_sequence_counters_next_value", "next_value >= 1"));
        b.Property(c => c.Key).HasMaxLength(40);
        b.Property(c => c.PeriodKey).HasMaxLength(12);
        b.HasIndex(c => new { c.SchoolId, c.Key, c.PeriodKey }).IsUnique();
    }
}

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_logs");
        b.Property(a => a.Action).HasMaxLength(60);
        b.Property(a => a.EntityType).HasMaxLength(80);
        b.Property(a => a.EntityId).HasMaxLength(80);
        b.Property(a => a.Changes).HasColumnType("jsonb");
        b.Property(a => a.IpAddress).HasMaxLength(64);
        b.Property(a => a.UserAgent).HasMaxLength(256);
        b.HasIndex(a => new { a.SchoolId, a.EntityType, a.EntityId });
        b.HasIndex(a => new { a.SchoolId, a.OccurredAt }).IsDescending(false, true);
        b.HasIndex(a => new { a.UserId, a.OccurredAt });
    }
}
