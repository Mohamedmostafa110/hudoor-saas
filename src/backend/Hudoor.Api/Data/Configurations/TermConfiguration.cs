using Hudoor.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hudoor.Api.Data.Configurations;

public class TermConfiguration : IEntityTypeConfiguration<Term>
{
    public void Configure(EntityTypeBuilder<Term> builder)
    {
        builder.ToTable("Terms");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.IsActive)
            .HasDefaultValue(false);

        builder.Property(t => t.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(t => t.TeacherId);
        builder.HasIndex(t => t.AcademicYearId);

        builder.HasOne(t => t.Teacher)
            .WithMany(tch => tch.Terms)
            .HasForeignKey(t => t.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.AcademicYear)
            .WithMany(ay => ay.Terms)
            .HasForeignKey(t => t.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
