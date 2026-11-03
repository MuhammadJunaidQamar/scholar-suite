using Scholar.Domain.Access;
using Scholar.Domain.Common;
using Scholar.Domain.Organization;

namespace Scholar.UnitTests.Domain;

public sealed class GradingScaleTests
{
    private static GradingScale Standard()
    {
        var scale = new GradingScale(Guid.NewGuid(), "Standard", GradingScaleType.Percentage);
        scale.ReplaceBands([
            (80m, 100m, "A", 4m, null, true),
            (60m, 79.99m, "B", 3m, null, true),
            (33m, 59.99m, "C", 2m, null, true),
            (0m, 32.99m, "F", 0m, null, false),
        ]);
        return scale;
    }

    [Theory]
    [InlineData(100, "A")]
    [InlineData(80, "A")]
    [InlineData(79.995, "B")] // values in the gap between bands resolve to the band below
    [InlineData(33, "C")]
    [InlineData(32.99, "F")]
    [InlineData(0, "F")]
    public void Resolves_percentage_to_band(decimal percentage, string expected) =>
        Assert.Equal(expected, Standard().Resolve(percentage)!.Grade);

    [Fact]
    public void Rejects_overlapping_bands()
    {
        var scale = new GradingScale(Guid.NewGuid(), "Bad", GradingScaleType.Percentage);
        var ex = Assert.Throws<DomainException>(() => scale.ReplaceBands([
            (50m, 100m, "P", null, null, true),
            (0m, 50m, "F", null, null, false),
        ]));
        Assert.Equal("grading.overlap", ex.Code);
    }

    [Fact]
    public void Rejects_out_of_range_bands() =>
        Assert.Throws<DomainException>(() => new GradingScale(Guid.NewGuid(), "Bad", GradingScaleType.Percentage)
            .ReplaceBands([(0m, 120m, "X", null, null, true)]));
}

public sealed class AcademicYearTests
{
    private static AcademicYear Year() => new(Guid.NewGuid(), "2026–27", new DateOnly(2026, 8, 1), new DateOnly(2027, 7, 31));

    [Fact]
    public void Terms_must_be_inside_the_year_and_not_overlap()
    {
        var year = Year();
        year.AddTerm("First", new DateOnly(2026, 8, 3), new DateOnly(2026, 12, 18));

        Assert.Equal("term.overlap",
            Assert.Throws<DomainException>(() => year.AddTerm("Clash", new DateOnly(2026, 12, 1), new DateOnly(2027, 1, 30))).Code);
        Assert.Equal("term.outside_year",
            Assert.Throws<DomainException>(() => year.AddTerm("Late", new DateOnly(2027, 7, 1), new DateOnly(2027, 8, 15))).Code);

        var second = year.AddTerm("Second", new DateOnly(2027, 1, 4), new DateOnly(2027, 6, 4));
        Assert.Equal(2, second.Sequence);
    }

    [Fact]
    public void Current_year_cannot_be_closed_and_closed_year_cannot_become_current()
    {
        var year = Year();
        year.MakeCurrent();
        Assert.Throws<DomainException>(year.Close);

        year.ClearCurrent();
        year.Close();
        Assert.Throws<DomainException>(year.MakeCurrent);
    }

    [Fact]
    public void End_must_follow_start() =>
        Assert.Throws<DomainException>(() => new AcademicYear(Guid.NewGuid(), "x", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 1)));
}

public sealed class RoleTests
{
    [Fact]
    public void SetPermissions_replaces_the_set_and_rejects_unknown_keys()
    {
        var role = new Role(Guid.NewGuid(), "Custom", Portal.Admin);
        role.SetPermissions([Permissions.Students.View, Permissions.Students.Update]);
        role.SetPermissions([Permissions.Students.View, Permissions.Fees.View]);

        Assert.Equal([Permissions.Fees.View, Permissions.Students.View], role.Permissions.Select(p => p.Permission).Order());
        Assert.Equal("role.unknown_permission",
            Assert.Throws<DomainException>(() => role.SetPermissions(["students.teleport"])).Code);
    }

    [Fact]
    public void Locked_roles_cannot_be_edited()
    {
        var role = new Role(Guid.NewGuid(), "Owner", Portal.Admin, SystemRoles.Owner, isSystem: true, isLocked: true);
        Assert.Throws<DomainException>(() => role.SetPermissions([Permissions.Students.View]));
    }

    [Fact]
    public void Every_system_role_uses_only_catalogued_permissions_and_catalogue_keys_are_unique()
    {
        Assert.Equal(PermissionCatalog.All.Count, PermissionCatalog.Keys.Count);
        foreach (var role in SystemRoles.All)
        {
            Assert.All(role.Permissions, p => Assert.True(PermissionCatalog.Exists(p), $"{role.Key}: {p}"));
        }
    }

    [Fact]
    public void Every_permission_constant_is_in_the_catalogue()
    {
        var constants = typeof(Permissions).GetNestedTypes()
            .SelectMany(t => t.GetFields())
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();
        Assert.All(constants, c => Assert.True(PermissionCatalog.Exists(c), c));
        Assert.Equal(constants.Count, PermissionCatalog.Keys.Count);
    }
}
