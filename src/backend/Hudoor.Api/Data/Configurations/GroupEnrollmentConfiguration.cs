using Hudoor.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hudoor.Api.Data.Configurations;

public class GroupEnrollmentConfiguration : IEntityTypeConfiguration<GroupEnrollment>
{
    public void Configure(EntityTypeBuilder<GroupEnrollment> builder)
    {
        builder.ToTable("GroupEnrollments");

        builder.HasKey(ge => ge.Id);

        builder.Property(ge => ge.JoinedDateUtc)
            .IsRequired();

        builder.Property(ge => ge.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(ge => ge.TeacherId);
        builder.HasIndex(ge => ge.GroupId);
        builder.HasIndex(ge => ge.StudentId);

        // Prevent duplicate active enrollments for the same student in the same group
        builder.HasIndex(ge => new { ge.GroupId, ge.StudentId })
            .HasFilter("[IsActive] = 1")
            .HasDatabaseName("IX_GroupEnrollments_GroupId_StudentId_Active");

        builder.HasOne(ge => ge.Teacher)
            .WithMany(t => t.Enrollments)
            .HasForeignKey(ge => ge.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ge => ge.Group)
            .WithMany(g => g.Enrollments)
            .HasForeignKey(ge => ge.GroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ge => ge.Student)
            .WithMany(s => s.Enrollments)
            .HasForeignKey(ge => ge.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
