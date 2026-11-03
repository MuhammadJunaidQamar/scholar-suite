using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scholar.Domain.Attendance;
using Scholar.Domain.Communication;
using Scholar.Domain.Documents;
using Scholar.Domain.Organization;
using Scholar.Domain.Platform;
using Scholar.Domain.Staffing;
using Scholar.Domain.Students;

namespace Scholar.Infrastructure.Persistence.Configurations;

internal sealed class AttendanceRegisterConfiguration : IEntityTypeConfiguration<AttendanceRegister>
{
    public void Configure(EntityTypeBuilder<AttendanceRegister> b)
    {
        b.ToTable("attendance_registers", t => t.HasCheckConstraint("ck_attendance_registers_period", "period IS NULL OR period BETWEEN 1 AND 16"));
        b.Property(r => r.Note).HasMaxLength(500);
        b.Ignore(r => r.IsDaily);
        // One daily roll call per section per day, and one register per period: enforced by the database, so two
        // teachers submitting at once cannot create duplicates.
        b.HasIndex(r => new { r.SectionId, r.Date }, "ux_attendance_registers_daily").IsUnique().HasFilter("period IS NULL");
        b.HasIndex(r => new { r.SectionId, r.Date, r.Period }, "ux_attendance_registers_period").IsUnique().HasFilter("period IS NOT NULL");
        b.HasIndex(r => new { r.SchoolId, r.Date, r.CampusId });
        b.HasOne<Section>().WithMany().HasForeignKey(r => r.SectionId);
        b.HasOne<AcademicYear>().WithMany().HasForeignKey(r => r.AcademicYearId);
        b.HasOne<Campus>().WithMany().HasForeignKey(r => r.CampusId);
        b.HasOne<Subject>().WithMany().HasForeignKey(r => r.SubjectId);
        b.HasOne<StaffMember>().WithMany().HasForeignKey(r => r.TakenByStaffId).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(r => r.Records).WithOne(x => x.Register).HasForeignKey(x => x.RegisterId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> b)
    {
        b.ToTable("attendance_records");
        b.Property(r => r.Remark).HasMaxLength(300);
        b.HasIndex(r => new { r.RegisterId, r.StudentId }).IsUnique();
        b.HasIndex(r => new { r.StudentId, r.Status });
        b.HasOne<Student>().WithMany().HasForeignKey(r => r.StudentId);
        b.HasOne<StudentLeaveRequest>().WithMany().HasForeignKey(r => r.LeaveRequestId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class AttendanceCorrectionConfiguration : IEntityTypeConfiguration<AttendanceCorrection>
{
    public void Configure(EntityTypeBuilder<AttendanceCorrection> b)
    {
        b.ToTable("attendance_corrections");
        b.Property(c => c.Reason).HasMaxLength(1000);
        b.Property(c => c.DecisionNote).HasMaxLength(1000);
        b.HasIndex(c => new { c.SchoolId, c.Status });
        b.HasIndex(c => c.RegisterId);
        b.HasOne<AttendanceRegister>().WithMany().HasForeignKey(c => c.RegisterId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(c => c.Items).WithOne().HasForeignKey(i => i.CorrectionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AttendanceCorrectionItemConfiguration : IEntityTypeConfiguration<AttendanceCorrectionItem>
{
    public void Configure(EntityTypeBuilder<AttendanceCorrectionItem> b)
    {
        b.ToTable("attendance_correction_items");
        b.Property(i => i.Remark).HasMaxLength(300);
        b.HasIndex(i => new { i.CorrectionId, i.StudentId }).IsUnique();
        b.HasOne<Student>().WithMany().HasForeignKey(i => i.StudentId);
    }
}

internal sealed class StudentLeaveRequestConfiguration : IEntityTypeConfiguration<StudentLeaveRequest>
{
    public void Configure(EntityTypeBuilder<StudentLeaveRequest> b)
    {
        b.ToTable("student_leave_requests", t => t.HasCheckConstraint("ck_student_leave_requests_dates", "to_date >= from_date"));
        b.Property(l => l.Reason).HasMaxLength(1000);
        b.Property(l => l.RequestedBy).HasMaxLength(150);
        b.Property(l => l.DecisionNote).HasMaxLength(1000);
        b.HasIndex(l => new { l.StudentId, l.FromDate });
        b.HasIndex(l => new { l.SchoolId, l.Status });
        b.HasOne<Student>().WithMany().HasForeignKey(l => l.StudentId);
        b.HasOne<StoredFile>().WithMany().HasForeignKey(l => l.DocumentFileId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class BellScheduleConfiguration : IEntityTypeConfiguration<BellSchedule>
{
    public void Configure(EntityTypeBuilder<BellSchedule> b)
    {
        b.ToTable("bell_schedules");
        b.Property(s => s.Name).HasMaxLength(100);
        b.HasIndex(s => s.CampusId).IsUnique();
        b.HasOne<Campus>().WithMany().HasForeignKey(s => s.CampusId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(s => s.Periods).WithOne().HasForeignKey(p => p.ScheduleId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class BellPeriodConfiguration : IEntityTypeConfiguration<BellPeriod>
{
    public void Configure(EntityTypeBuilder<BellPeriod> b)
    {
        b.ToTable("bell_periods", t => t.HasCheckConstraint("ck_bell_periods_times", "end_time > start_time"));
        b.Property(p => p.Name).HasMaxLength(60);
        b.Ignore(p => p.IsBreak);
        b.HasIndex(p => new { p.ScheduleId, p.Sequence });
    }
}

internal sealed class StaffAttendanceRecordConfiguration : IEntityTypeConfiguration<StaffAttendanceRecord>
{
    public void Configure(EntityTypeBuilder<StaffAttendanceRecord> b)
    {
        b.ToTable("staff_attendance_records", t => t.HasCheckConstraint("ck_staff_attendance_records_times",
            "check_in IS NULL OR check_out IS NULL OR check_out > check_in"));
        b.Property(r => r.Remark).HasMaxLength(300);
        b.HasIndex(r => new { r.StaffId, r.Date }).IsUnique();
        b.HasIndex(r => new { r.SchoolId, r.Date, r.CampusId });
        b.HasOne<StaffMember>().WithMany().HasForeignKey(r => r.StaffId);
        b.HasOne<Campus>().WithMany().HasForeignKey(r => r.CampusId);
        b.HasOne<StaffLeaveRequest>().WithMany().HasForeignKey(r => r.LeaveRequestId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class StaffAttendanceCorrectionConfiguration : IEntityTypeConfiguration<StaffAttendanceCorrection>
{
    public void Configure(EntityTypeBuilder<StaffAttendanceCorrection> b)
    {
        b.ToTable("staff_attendance_corrections");
        b.Property(c => c.Reason).HasMaxLength(1000);
        b.Property(c => c.DecisionNote).HasMaxLength(1000);
        b.HasIndex(c => new { c.SchoolId, c.Status });
        b.HasIndex(c => new { c.StaffId, c.Date });
        b.HasOne<StaffMember>().WithMany().HasForeignKey(c => c.StaffId);
    }
}

internal sealed class LeaveTypeConfiguration : IEntityTypeConfiguration<LeaveType>
{
    public void Configure(EntityTypeBuilder<LeaveType> b)
    {
        b.ToTable("leave_types", t => t.HasCheckConstraint("ck_leave_types_days", "annual_days IS NULL OR annual_days >= 0"));
        b.Property(l => l.Code).HasMaxLength(10);
        b.Property(l => l.Name).HasMaxLength(60);
        b.Property(l => l.AnnualDays).HasPrecision(5, 1);
        b.HasIndex(l => new { l.SchoolId, l.Code }).IsUnique();
    }
}

internal sealed class StaffLeaveRequestConfiguration : IEntityTypeConfiguration<StaffLeaveRequest>
{
    public void Configure(EntityTypeBuilder<StaffLeaveRequest> b)
    {
        b.ToTable("staff_leave_requests", t =>
        {
            t.HasCheckConstraint("ck_staff_leave_requests_dates", "to_date >= from_date");
            t.HasCheckConstraint("ck_staff_leave_requests_days", "days > 0");
        });
        b.Property(l => l.Days).HasPrecision(5, 1);
        b.Property(l => l.Reason).HasMaxLength(1000);
        b.Property(l => l.DecisionNote).HasMaxLength(1000);
        b.HasIndex(l => new { l.StaffId, l.FromDate });
        b.HasIndex(l => new { l.SchoolId, l.Status });
        b.HasOne<StaffMember>().WithMany().HasForeignKey(l => l.StaffId);
        b.HasOne<LeaveType>().WithMany().HasForeignKey(l => l.LeaveTypeId);
        b.HasOne<StoredFile>().WithMany().HasForeignKey(l => l.DocumentFileId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class OutboundMessageConfiguration : IEntityTypeConfiguration<OutboundMessage>
{
    public void Configure(EntityTypeBuilder<OutboundMessage> b)
    {
        b.ToTable("outbound_messages");
        b.Property(m => m.Recipient).HasMaxLength(254);
        b.Property(m => m.RecipientName).HasMaxLength(200);
        b.Property(m => m.Purpose).HasMaxLength(60);
        b.Property(m => m.Subject).HasMaxLength(200);
        b.Property(m => m.Body).HasMaxLength(2000);
        b.Property(m => m.DedupKey).HasMaxLength(200);
        b.Property(m => m.StatusDetail).HasMaxLength(500);
        b.Property(m => m.Provider).HasMaxLength(60);
        b.Property(m => m.ProviderMessageId).HasMaxLength(120);
        b.Ignore(m => m.IsFinal);
        b.HasIndex(m => new { m.SchoolId, m.DedupKey }).IsUnique();
        b.HasIndex(m => m.Status).HasFilter("status = 'Pending'").HasDatabaseName("ix_outbound_messages_pending");
        b.HasIndex(m => new { m.SchoolId, m.RelatedId });
        b.HasIndex(m => new { m.SchoolId, m.StudentId, m.CreatedAt });
    }
}
