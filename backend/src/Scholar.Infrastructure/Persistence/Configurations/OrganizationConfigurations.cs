using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scholar.Domain.Organization;
using Scholar.Domain.Platform;

namespace Scholar.Infrastructure.Persistence.Configurations;

internal sealed class AcademicYearConfiguration : IEntityTypeConfiguration<AcademicYear>
{
    public void Configure(EntityTypeBuilder<AcademicYear> b)
    {
        b.ToTable("academic_years", t => t.HasCheckConstraint("ck_academic_years_dates", "end_date > start_date"));
        b.Property(y => y.Name).HasMaxLength(40);
        b.HasIndex(y => new { y.SchoolId, y.Name }).IsUnique();
        b.HasIndex(y => y.SchoolId, "ux_academic_years_one_current").IsUnique().HasFilter("is_current");
        b.HasMany(y => y.Terms).WithOne().HasForeignKey(t => t.AcademicYearId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AcademicTermConfiguration : IEntityTypeConfiguration<AcademicTerm>
{
    public void Configure(EntityTypeBuilder<AcademicTerm> b)
    {
        b.ToTable("academic_terms", t => t.HasCheckConstraint("ck_academic_terms_dates", "end_date > start_date"));
        b.Property(t => t.Name).HasMaxLength(60);
        // Sequence is a derived display order (date order); not unique, so terms can swap places in one save.
        b.HasIndex(t => new { t.AcademicYearId, t.Sequence });
    }
}

internal sealed class GradeConfiguration : IEntityTypeConfiguration<Grade>
{
    public void Configure(EntityTypeBuilder<Grade> b)
    {
        b.ToTable("grades");
        b.Property(g => g.Name).HasMaxLength(60);
        b.Property(g => g.Code).HasMaxLength(20);
        b.HasIndex(g => new { g.SchoolId, g.Code }).IsUnique();
        b.HasOne<Grade>().WithMany().HasForeignKey(g => g.NextGradeId);
    }
}

internal sealed class SectionConfiguration : IEntityTypeConfiguration<Section>
{
    public void Configure(EntityTypeBuilder<Section> b)
    {
        b.ToTable("sections", t => t.HasCheckConstraint("ck_sections_capacity", "capacity >= 0"));
        b.Property(s => s.Name).HasMaxLength(30);
        b.Ignore(s => s.DisplayName);
        b.HasOne(s => s.AcademicYear).WithMany().HasForeignKey(s => s.AcademicYearId);
        b.HasOne(s => s.Grade).WithMany().HasForeignKey(s => s.GradeId);
        b.HasOne(s => s.Room).WithMany().HasForeignKey(s => s.RoomId);
        b.HasOne<Campus>().WithMany().HasForeignKey(s => s.CampusId);
        b.HasOne<SubjectGroup>().WithMany().HasForeignKey(s => s.SubjectGroupId);
        b.HasOne<Domain.Staffing.StaffMember>().WithMany().HasForeignKey(s => s.ClassTeacherId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(s => new { s.AcademicYearId, s.CampusId, s.GradeId, s.Name }).IsUnique();
        b.HasIndex(s => s.ClassTeacherId);
    }
}

internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> b)
    {
        b.ToTable("rooms");
        b.Property(r => r.Code).HasMaxLength(20);
        b.Property(r => r.Name).HasMaxLength(100);
        b.Property(r => r.Building).HasMaxLength(80);
        b.Property(r => r.Floor).HasMaxLength(30);
        b.HasOne<Campus>().WithMany().HasForeignKey(r => r.CampusId);
        b.HasIndex(r => new { r.CampusId, r.Code }).IsUnique();
    }
}

internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.ToTable("departments");
        b.Property(d => d.Code).HasMaxLength(20);
        b.Property(d => d.Name).HasMaxLength(100);
        b.Property(d => d.Description).HasMaxLength(500);
        b.HasIndex(d => new { d.SchoolId, d.Code }).IsUnique();
        // No foreign key to staff: staff already reference departments, and a cycle would block bulk deletes/resets.
    }
}

internal sealed class DesignationConfiguration : IEntityTypeConfiguration<Designation>
{
    public void Configure(EntityTypeBuilder<Designation> b)
    {
        b.ToTable("designations");
        b.Property(d => d.Name).HasMaxLength(100);
        b.HasIndex(d => new { d.SchoolId, d.Name }).IsUnique();
    }
}

internal sealed class SubjectGroupConfiguration : IEntityTypeConfiguration<SubjectGroup>
{
    public void Configure(EntityTypeBuilder<SubjectGroup> b)
    {
        b.ToTable("subject_groups");
        b.Property(g => g.Name).HasMaxLength(100);
        b.Property(g => g.Description).HasMaxLength(500);
        b.HasIndex(g => new { g.SchoolId, g.Name }).IsUnique();
    }
}

internal sealed class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
    public void Configure(EntityTypeBuilder<Subject> b)
    {
        b.ToTable("subjects");
        b.Property(s => s.Code).HasMaxLength(20);
        b.Property(s => s.Name).HasMaxLength(100);
        b.Property(s => s.ShortName).HasMaxLength(30);
        b.Property(s => s.Color).HasMaxLength(7);
        b.HasOne<SubjectGroup>().WithMany().HasForeignKey(s => s.SubjectGroupId);
        b.HasIndex(s => new { s.SchoolId, s.Code }).IsUnique();
    }
}

internal sealed class GradeSubjectConfiguration : IEntityTypeConfiguration<GradeSubject>
{
    public void Configure(EntityTypeBuilder<GradeSubject> b)
    {
        b.ToTable("grade_subjects", t => t.HasCheckConstraint("ck_grade_subjects_periods", "periods_per_week BETWEEN 0 AND 40"));
        b.HasOne<AcademicYear>().WithMany().HasForeignKey(g => g.AcademicYearId);
        b.HasOne<Grade>().WithMany().HasForeignKey(g => g.GradeId);
        b.HasOne(g => g.Subject).WithMany().HasForeignKey(g => g.SubjectId);
        b.HasOne<SubjectGroup>().WithMany().HasForeignKey(g => g.SubjectGroupId);
        b.HasIndex(g => new { g.AcademicYearId, g.GradeId, g.SubjectId }).IsUnique();
    }
}

internal sealed class CalendarEventConfiguration : IEntityTypeConfiguration<CalendarEvent>
{
    public void Configure(EntityTypeBuilder<CalendarEvent> b)
    {
        b.ToTable("calendar_events", t => t.HasCheckConstraint("ck_calendar_events_dates", "end_date >= start_date"));
        b.Property(e => e.Title).HasMaxLength(200);
        b.Property(e => e.Description).HasMaxLength(4000);
        b.Property(e => e.Location).HasMaxLength(200);
        b.HasOne<Campus>().WithMany().HasForeignKey(e => e.CampusId);
        b.HasOne<Room>().WithMany().HasForeignKey(e => e.RoomId);
        b.HasIndex(e => new { e.SchoolId, e.StartDate, e.EndDate });
    }
}

internal sealed class GradingScaleConfiguration : IEntityTypeConfiguration<GradingScale>
{
    public void Configure(EntityTypeBuilder<GradingScale> b)
    {
        b.ToTable("grading_scales");
        b.Property(g => g.Name).HasMaxLength(100);
        b.HasIndex(g => new { g.SchoolId, g.Name }).IsUnique();
        b.HasMany(g => g.Bands).WithOne().HasForeignKey(x => x.GradingScaleId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class GradingBandConfiguration : IEntityTypeConfiguration<GradingBand>
{
    public void Configure(EntityTypeBuilder<GradingBand> b)
    {
        b.ToTable("grading_bands", t => t.HasCheckConstraint("ck_grading_bands_range",
            "min_percent >= 0 AND max_percent <= 100 AND min_percent <= max_percent"));
        b.Property(x => x.Grade).HasMaxLength(10);
        b.Property(x => x.Remark).HasMaxLength(60);
        b.Property(x => x.MinPercent).HasPrecision(5, 2);
        b.Property(x => x.MaxPercent).HasPrecision(5, 2);
        b.Property(x => x.GradePoint).HasPrecision(4, 2);
    }
}
