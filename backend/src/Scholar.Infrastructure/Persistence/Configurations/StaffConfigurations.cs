using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scholar.Domain.Documents;
using Scholar.Domain.Organization;
using Scholar.Domain.Platform;
using Scholar.Domain.Staffing;

namespace Scholar.Infrastructure.Persistence.Configurations;

internal sealed class StaffMemberConfiguration : IEntityTypeConfiguration<StaffMember>
{
    public void Configure(EntityTypeBuilder<StaffMember> b)
    {
        b.ToTable("staff");
        b.Property(s => s.EmployeeNumber).HasMaxLength(40);
        b.Property(s => s.FirstName).HasMaxLength(100);
        b.Property(s => s.LastName).HasMaxLength(100);
        b.Property(s => s.FullName).HasMaxLength(210).HasComputedColumnSql("trim(first_name || ' ' || last_name)", stored: true);
        b.Property(s => s.NationalId).HasMaxLength(20);
        b.Property(s => s.Phone).HasMaxLength(20);
        b.Property(s => s.Email).HasMaxLength(254);
        b.Property(s => s.ExitReason).HasMaxLength(500);
        b.OwnsAddress(s => s.Address);
        b.Ignore(s => s.IsCurrent);

        b.HasIndex(s => new { s.SchoolId, s.EmployeeNumber }).IsUnique();
        b.HasIndex(s => new { s.SchoolId, s.Status, s.CampusId });
        b.HasIndex(s => s.FullName).HasMethod("gin").HasOperators("gin_trgm_ops").HasDatabaseName("ix_staff_full_name_trgm");
        // One staff record per sign-in account within a school.
        b.HasIndex(s => new { s.SchoolId, s.UserId }).IsUnique().HasFilter("user_id IS NOT NULL");

        b.HasOne<Campus>().WithMany().HasForeignKey(s => s.CampusId);
        b.HasOne<Department>().WithMany().HasForeignKey(s => s.DepartmentId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<Designation>().WithMany().HasForeignKey(s => s.DesignationId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<StoredFile>().WithMany().HasForeignKey(s => s.PhotoFileId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<Domain.Identity.User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class SectionSubjectTeacherConfiguration : IEntityTypeConfiguration<SectionSubjectTeacher>
{
    public void Configure(EntityTypeBuilder<SectionSubjectTeacher> b)
    {
        b.ToTable("section_subject_teachers");
        b.HasIndex(t => new { t.SectionId, t.SubjectId }).IsUnique();
        b.HasIndex(t => t.StaffId);
        b.HasOne<Section>().WithMany().HasForeignKey(t => t.SectionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Subject>().WithMany().HasForeignKey(t => t.SubjectId);
        b.HasOne<StaffMember>().WithMany().HasForeignKey(t => t.StaffId);
    }
}
