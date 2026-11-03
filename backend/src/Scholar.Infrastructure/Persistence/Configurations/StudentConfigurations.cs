using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scholar.Domain.Admissions;
using Scholar.Domain.Documents;
using Scholar.Domain.Organization;
using Scholar.Domain.Platform;
using Scholar.Domain.Students;

namespace Scholar.Infrastructure.Persistence.Configurations;

internal sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> b)
    {
        b.ToTable("students");
        b.Property(s => s.AdmissionNumber).HasMaxLength(40);
        b.Property(s => s.RegistrationNumber).HasMaxLength(40);
        b.Property(s => s.FirstName).HasMaxLength(100);
        b.Property(s => s.MiddleName).HasMaxLength(100);
        b.Property(s => s.LastName).HasMaxLength(100);
        b.Property(s => s.FullName).HasMaxLength(320)
            .HasComputedColumnSql("first_name || coalesce(' ' || middle_name, '') || ' ' || last_name", stored: true);
        b.Property(s => s.PlaceOfBirth).HasMaxLength(100);
        b.Property(s => s.NationalId).HasMaxLength(20);
        b.Property(s => s.Nationality).HasMaxLength(60);
        b.Property(s => s.Religion).HasMaxLength(40);
        b.Property(s => s.BloodGroup).HasMaxLength(5);
        b.Property(s => s.MotherTongue).HasMaxLength(40);
        b.Property(s => s.Email).HasMaxLength(254);
        b.Property(s => s.Phone).HasMaxLength(40);
        b.Property(s => s.PreviousSchoolName).HasMaxLength(200);
        b.Property(s => s.PreviousSchoolClass).HasMaxLength(60);
        b.Property(s => s.MedicalAlert).HasMaxLength(300);
        b.OwnsAddress(s => s.Address);
        b.Ignore(s => s.IsCurrent);

        b.HasIndex(s => new { s.SchoolId, s.AdmissionNumber }).IsUnique();
        b.HasIndex(s => new { s.SchoolId, s.Status, s.CampusId });
        b.HasIndex(s => s.FullName).HasMethod("gin").HasOperators("gin_trgm_ops").HasDatabaseName("ix_students_full_name_trgm");
        b.HasIndex(s => new { s.SchoolId, s.NationalId });
        b.HasIndex(s => s.UserId).IsUnique().HasFilter("user_id IS NOT NULL");

        b.HasOne<Campus>().WithMany().HasForeignKey(s => s.CampusId);
        b.HasOne<Section>().WithMany().HasForeignKey(s => s.CurrentSectionId);
        b.HasOne<AcademicYear>().WithMany().HasForeignKey(s => s.CurrentAcademicYearId);
        b.HasOne<StoredFile>().WithMany().HasForeignKey(s => s.PhotoFileId);
        b.HasOne<Domain.Identity.User>().WithMany().HasForeignKey(s => s.UserId);

        b.HasMany(s => s.Enrollments).WithOne().HasForeignKey(e => e.StudentId);
        b.HasMany(s => s.Guardians).WithOne(g => g.Student).HasForeignKey(g => g.StudentId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(s => s.EmergencyContacts).WithOne().HasForeignKey(c => c.StudentId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> b)
    {
        b.ToTable("enrollments", t => t.HasCheckConstraint("ck_enrollments_dates", "end_date IS NULL OR end_date >= start_date"));
        b.Property(e => e.RollNumber).HasMaxLength(20);
        b.Property(e => e.Reason).HasMaxLength(500);
        b.HasOne<AcademicYear>().WithMany().HasForeignKey(e => e.AcademicYearId);
        b.HasOne<Campus>().WithMany().HasForeignKey(e => e.CampusId);
        b.HasOne<Grade>().WithMany().HasForeignKey(e => e.GradeId);
        b.HasOne<Section>().WithMany().HasForeignKey(e => e.SectionId);
        // One active placement per student per year; history rows (closed enrollments) are unlimited.
        b.HasIndex(e => new { e.StudentId, e.AcademicYearId }, "ux_enrollments_one_active_per_year").IsUnique().HasFilter("status = 'Active'");
        b.HasIndex(e => new { e.SectionId, e.Status });
    }
}

internal sealed class StudentStatusChangeConfiguration : IEntityTypeConfiguration<StudentStatusChange>
{
    public void Configure(EntityTypeBuilder<StudentStatusChange> b)
    {
        b.ToTable("student_status_changes");
        b.Property(c => c.Reason).HasMaxLength(500);
        b.Property(c => c.DestinationSchool).HasMaxLength(200);
        b.HasOne<Student>().WithMany().HasForeignKey(c => c.StudentId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class StudentMedicalProfileConfiguration : IEntityTypeConfiguration<StudentMedicalProfile>
{
    public void Configure(EntityTypeBuilder<StudentMedicalProfile> b)
    {
        b.ToTable("student_medical_profiles");
        foreach (var name in new[] { "Allergies", "Conditions", "Medications", "SpecialNeeds", "EmergencyInstructions" })
        {
            b.Property(name).HasMaxLength(2000);
        }

        b.Property(m => m.DoctorName).HasMaxLength(150);
        b.Property(m => m.DoctorPhone).HasMaxLength(40);
        b.HasIndex(m => m.StudentId).IsUnique();
        b.HasOne<Student>().WithMany().HasForeignKey(m => m.StudentId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class EmergencyContactConfiguration : IEntityTypeConfiguration<EmergencyContact>
{
    public void Configure(EntityTypeBuilder<EmergencyContact> b)
    {
        b.ToTable("emergency_contacts");
        b.Property(c => c.Name).HasMaxLength(150);
        b.Property(c => c.Relationship).HasMaxLength(60);
        b.Property(c => c.Phone).HasMaxLength(40);
        b.Property(c => c.AlternatePhone).HasMaxLength(40);
    }
}

internal sealed class GuardianConfiguration : IEntityTypeConfiguration<Guardian>
{
    public void Configure(EntityTypeBuilder<Guardian> b)
    {
        b.ToTable("guardians");
        b.Property(g => g.FirstName).HasMaxLength(100);
        b.Property(g => g.LastName).HasMaxLength(100);
        b.Property(g => g.FullName).HasMaxLength(210).HasComputedColumnSql("trim(first_name || ' ' || last_name)", stored: true);
        b.Property(g => g.NationalId).HasMaxLength(20);
        b.Property(g => g.Email).HasMaxLength(254);
        b.Property(g => g.Phone).HasMaxLength(20);
        b.Property(g => g.AlternatePhone).HasMaxLength(40);
        b.Property(g => g.Occupation).HasMaxLength(100);
        b.Property(g => g.Employer).HasMaxLength(150);
        b.Property(g => g.PreferredLanguage).HasMaxLength(20);
        b.OwnsAddress(g => g.Address);
        b.HasIndex(g => new { g.SchoolId, g.Phone });
        b.HasIndex(g => new { g.SchoolId, g.NationalId });
        b.HasIndex(g => g.FullName).HasMethod("gin").HasOperators("gin_trgm_ops").HasDatabaseName("ix_guardians_full_name_trgm");
        b.HasIndex(g => new { g.SchoolId, g.UserId }).IsUnique().HasFilter("user_id IS NOT NULL");
        b.HasOne<Domain.Identity.User>().WithMany().HasForeignKey(g => g.UserId);
        b.HasMany(g => g.Students).WithOne(l => l.Guardian).HasForeignKey(l => l.GuardianId);
    }
}

internal sealed class StudentGuardianConfiguration : IEntityTypeConfiguration<StudentGuardian>
{
    public void Configure(EntityTypeBuilder<StudentGuardian> b)
    {
        b.ToTable("student_guardians");
        b.HasIndex(l => new { l.StudentId, l.GuardianId }).IsUnique();
        b.HasIndex(l => l.GuardianId);
    }
}

internal sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> b)
    {
        b.ToTable("stored_files", t => t.HasCheckConstraint("ck_stored_files_size", "size_bytes >= 0"));
        b.Property(f => f.OwnerType).HasMaxLength(30);
        b.Property(f => f.Category).HasMaxLength(40);
        b.Property(f => f.FileName).HasMaxLength(255);
        b.Property(f => f.ContentType).HasMaxLength(120);
        b.Property(f => f.Sha256).HasMaxLength(64);
        b.Property(f => f.StorageKey).HasMaxLength(200);
        b.Property(f => f.Notes).HasMaxLength(500);
        b.HasIndex(f => new { f.SchoolId, f.OwnerType, f.OwnerId });
        b.HasIndex(f => f.StorageKey).IsUnique();
    }
}

internal sealed class AdmissionInquiryConfiguration : IEntityTypeConfiguration<AdmissionInquiry>
{
    public void Configure(EntityTypeBuilder<AdmissionInquiry> b)
    {
        b.ToTable("admission_inquiries");
        b.Property(i => i.StudentName).HasMaxLength(200);
        b.Property(i => i.GuardianName).HasMaxLength(200);
        b.Property(i => i.Phone).HasMaxLength(20);
        b.Property(i => i.Email).HasMaxLength(254);
        b.Property(i => i.Notes).HasMaxLength(2000);
        b.Property(i => i.ClosedReason).HasMaxLength(300);
        b.HasOne<Campus>().WithMany().HasForeignKey(i => i.CampusId);
        b.HasOne<Grade>().WithMany().HasForeignKey(i => i.GradeId);
        b.HasOne<AcademicYear>().WithMany().HasForeignKey(i => i.AcademicYearId);
        b.HasIndex(i => new { i.SchoolId, i.Status, i.FollowUpOn });
        b.HasIndex(i => new { i.SchoolId, i.Phone });
    }
}

internal sealed class AdmissionApplicationConfiguration : IEntityTypeConfiguration<AdmissionApplication>
{
    public void Configure(EntityTypeBuilder<AdmissionApplication> b)
    {
        b.ToTable("admission_applications");
        b.Property(a => a.ApplicationNumber).HasMaxLength(40);
        foreach (var name in new[] { "FirstName", "MiddleName", "LastName", "GuardianFirstName", "GuardianLastName" })
        {
            b.Property(name).HasMaxLength(100);
        }

        b.Property(a => a.NationalId).HasMaxLength(20);
        b.Property(a => a.PreviousSchoolName).HasMaxLength(200);
        b.Property(a => a.PreviousSchoolClass).HasMaxLength(60);
        b.Property(a => a.PreviousResult).HasMaxLength(100);
        b.Property(a => a.GuardianPhone).HasMaxLength(20);
        b.Property(a => a.GuardianEmail).HasMaxLength(254);
        b.Property(a => a.GuardianNationalId).HasMaxLength(20);
        b.Property(a => a.GuardianOccupation).HasMaxLength(100);
        b.Property(a => a.Notes).HasMaxLength(2000);
        b.Property(a => a.DecisionNote).HasMaxLength(1000);
        b.OwnsAddress(a => a.Address);
        b.Ignore(a => a.ApplicantName);
        b.Ignore(a => a.IsOpen);

        b.HasIndex(a => new { a.SchoolId, a.ApplicationNumber }).IsUnique();
        b.HasIndex(a => new { a.SchoolId, a.Status, a.AcademicYearId });
        b.HasIndex(a => new { a.SchoolId, a.GuardianPhone });
        b.HasOne<AcademicYear>().WithMany().HasForeignKey(a => a.AcademicYearId);
        b.HasOne<Campus>().WithMany().HasForeignKey(a => a.CampusId);
        b.HasOne<Grade>().WithMany().HasForeignKey(a => a.GradeId);
        b.HasOne<AdmissionInquiry>().WithMany().HasForeignKey(a => a.InquiryId);
        b.HasOne<Student>().WithMany().HasForeignKey(a => a.StudentId);
        b.HasMany(a => a.History).WithOne().HasForeignKey(h => h.ApplicationId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(a => a.Assessments).WithOne().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ApplicationStatusChangeConfiguration : IEntityTypeConfiguration<ApplicationStatusChange>
{
    public void Configure(EntityTypeBuilder<ApplicationStatusChange> b)
    {
        b.ToTable("application_status_changes");
        b.Property(h => h.Note).HasMaxLength(1000);
        b.HasIndex(h => new { h.ApplicationId, h.ChangedAt });
    }
}

internal sealed class ApplicationAssessmentConfiguration : IEntityTypeConfiguration<ApplicationAssessment>
{
    public void Configure(EntityTypeBuilder<ApplicationAssessment> b)
    {
        b.ToTable("application_assessments");
        b.Property(a => a.Location).HasMaxLength(200);
        b.Property(a => a.Notes).HasMaxLength(2000);
        b.Property(a => a.Score).HasPrecision(7, 2);
        b.Property(a => a.MaxScore).HasPrecision(7, 2);
    }
}
