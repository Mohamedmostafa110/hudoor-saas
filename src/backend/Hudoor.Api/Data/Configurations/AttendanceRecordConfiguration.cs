using Hudoor.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hudoor.Api.Data.Configurations;

public class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("AttendanceRecords");

        builder.HasKey(ar => ar.Id);

        builder.Property(ar => ar.Status)
            .HasMaxLength(50)
            .IsRequired()
            .IsUnicode(false);

        builder.Property(ar => ar.Method)
            .HasMaxLength(50)
            .IsRequired()
            .IsUnicode(false);

        builder.Property(ar => ar.RecordedAtUtc)
            .IsRequired();

        builder.Property(ar => ar.Notes)
            .HasMaxLength(500);

        // Core business constraint: exactly one attendance record per student per session
        builder.HasIndex(ar => new { ar.SessionId, ar.StudentId })
            .IsUnique()
            .HasDatabaseName("IX_AttendanceRecords_SessionId_StudentId");

        builder.HasIndex(ar => ar.TeacherId);
        builder.HasIndex(ar => ar.StudentId);
        builder.HasIndex(ar => ar.RecordedAtUtc);

        builder.HasOne(ar => ar.Teacher)
            .WithMany(t => t.AttendanceRecords)
            .HasForeignKey(ar => ar.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ar => ar.Session)
            .WithMany(s => s.AttendanceRecords)
            .HasForeignKey(ar => ar.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ar => ar.Student)
            .WithMany(st => st.AttendanceRecords)
            .HasForeignKey(ar => ar.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
