using Hudoor.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hudoor.Api.Data.Configurations;

public class AttendanceAuditLogConfiguration : IEntityTypeConfiguration<AttendanceAuditLog>
{
    public void Configure(EntityTypeBuilder<AttendanceAuditLog> builder)
    {
        builder.ToTable("AttendanceAuditLogs");

        builder.HasKey(aal => aal.Id);

        builder.Property(aal => aal.PreviousStatus)
            .HasMaxLength(50)
            .IsRequired()
            .IsUnicode(false);

        builder.Property(aal => aal.NewStatus)
            .HasMaxLength(50)
            .IsRequired()
            .IsUnicode(false);

        builder.Property(aal => aal.TimestampUtc)
            .IsRequired();

        builder.Property(aal => aal.ModificationReason)
            .HasMaxLength(500);

        builder.HasIndex(aal => aal.AttendanceRecordId);
        builder.HasIndex(aal => aal.ModifiedByTeacherId);
        builder.HasIndex(aal => aal.TimestampUtc);

        builder.HasOne(aal => aal.AttendanceRecord)
            .WithMany(ar => ar.AuditLogs)
            .HasForeignKey(aal => aal.AttendanceRecordId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(aal => aal.ModifiedByTeacher)
            .WithMany()
            .HasForeignKey(aal => aal.ModifiedByTeacherId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
