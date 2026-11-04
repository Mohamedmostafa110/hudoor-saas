using Hudoor.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hudoor.Api.Data.Configurations;

public class AcademicYearConfiguration : IEntityTypeConfiguration<AcademicYear>
{
    public void Configure(EntityTypeBuilder<AcademicYear> builder)
    {
        builder.ToTable("AcademicYears");

        builder.HasKey(ay => ay.Id);

        builder.Property(ay => ay.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(ay => ay.IsActive)
            .HasDefaultValue(false);

        builder.Property(ay => ay.IsArchived)
            .HasDefaultValue(false);

        builder.Property(ay => ay.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(ay => ay.TeacherId);

        builder.HasOne(ay => ay.Teacher)
            .WithMany(t => t.AcademicYears)
            .HasForeignKey(ay => ay.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
