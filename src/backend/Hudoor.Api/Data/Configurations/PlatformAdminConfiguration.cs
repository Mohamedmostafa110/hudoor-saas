using Hudoor.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hudoor.Api.Data.Configurations;

public class PlatformAdminConfiguration : IEntityTypeConfiguration<PlatformAdmin>
{
    public void Configure(EntityTypeBuilder<PlatformAdmin> builder)
    {
        builder.ToTable("PlatformAdmins");

        builder.HasKey(pa => pa.Id);

        builder.Property(pa => pa.Username)
            .HasMaxLength(100)
            .IsRequired()
            .IsUnicode(false);

        builder.Property(pa => pa.PasswordHash)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(pa => pa.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(pa => pa.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(pa => pa.IsActive)
            .HasDefaultValue(true);

        builder.Property(pa => pa.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(pa => pa.Username)
            .IsUnique();
    }
}
