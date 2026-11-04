using Hudoor.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hudoor.Api.Data.Configurations;

public class TeacherConfiguration : IEntityTypeConfiguration<Teacher>
{
    public void Configure(EntityTypeBuilder<Teacher> builder)
    {
        builder.ToTable("Teachers");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.Username)
            .HasMaxLength(100)
            .IsRequired()
            .IsUnicode(false);

        builder.Property(t => t.PasswordHash)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(t => t.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(t => t.PhoneNumber)
            .HasMaxLength(50);

        builder.Property(t => t.BrandName)
            .HasMaxLength(200);

        builder.Property(t => t.IsActive)
            .HasDefaultValue(true);

        builder.Property(t => t.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(t => t.Username)
            .IsUnique();

        builder.HasIndex(t => t.Email);
    }
}
