using Hudoor.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hudoor.Api.Data.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Students");

        builder.HasKey(s => s.Id);

        // Global uniqueness across entire platform as required for single-screen login
        builder.Property(s => s.StudentCode)
            .HasMaxLength(50)
            .IsRequired()
            .IsUnicode(false);

        builder.Property(s => s.PasswordHash)
            .HasMaxLength(500)
            .IsRequired()
            .IsUnicode(false);

        builder.Property(s => s.TokenVersion)
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(s => s.MustChangePassword)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(s => s.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.PhoneNumber)
            .HasMaxLength(50);

        builder.Property(s => s.ParentPhoneNumber)
            .HasMaxLength(50);

        builder.Property(s => s.CurrentGrade)
            .HasMaxLength(100);

        builder.Property(s => s.Notes)
            .HasMaxLength(1000);

        builder.Property(s => s.IsActive)
            .HasDefaultValue(true);

        builder.Property(s => s.CreatedAtUtc)
            .IsRequired();

        // Enforce global uniqueness on StudentCode
        builder.HasIndex(s => s.StudentCode)
            .IsUnique();

        builder.HasIndex(s => s.TeacherId);

        builder.HasOne(s => s.Teacher)
            .WithMany(t => t.Students)
            .HasForeignKey(s => s.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
