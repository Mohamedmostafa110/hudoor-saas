using Hudoor.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hudoor.Api.Data.Configurations;

public class PasswordResetAuditLogConfiguration : IEntityTypeConfiguration<PasswordResetAuditLog>
{
    public void Configure(EntityTypeBuilder<PasswordResetAuditLog> builder)
    {
        builder.ToTable("PasswordResetAuditLogs");

        builder.HasKey(pral => pral.Id);

        builder.Property(pral => pral.TimestampUtc)
            .IsRequired();

        builder.Property(pral => pral.ClientIpAddress)
            .HasMaxLength(100)
            .IsUnicode(false);

        builder.HasIndex(pral => pral.StudentId);
        builder.HasIndex(pral => pral.TriggeredByTeacherId);
        builder.HasIndex(pral => pral.TimestampUtc);

        builder.HasOne(pral => pral.Student)
            .WithMany(s => s.PasswordResetAuditLogs)
            .HasForeignKey(pral => pral.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pral => pral.TriggeredByTeacher)
            .WithMany()
            .HasForeignKey(pral => pral.TriggeredByTeacherId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
