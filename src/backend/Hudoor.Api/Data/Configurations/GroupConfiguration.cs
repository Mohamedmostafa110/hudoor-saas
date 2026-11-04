using Hudoor.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hudoor.Api.Data.Configurations;

public class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("Groups");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(g => g.ScheduleDescription)
            .HasMaxLength(300);

        builder.Property(g => g.IsArchived)
            .HasDefaultValue(false);

        builder.Property(g => g.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(g => g.TeacherId);
        builder.HasIndex(g => g.SubjectId);
        builder.HasIndex(g => g.AcademicYearId);

        builder.HasOne(g => g.Teacher)
            .WithMany(t => t.Groups)
            .HasForeignKey(g => g.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(g => g.Subject)
            .WithMany(s => s.Groups)
            .HasForeignKey(g => g.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(g => g.AcademicYear)
            .WithMany(ay => ay.Groups)
            .HasForeignKey(g => g.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
