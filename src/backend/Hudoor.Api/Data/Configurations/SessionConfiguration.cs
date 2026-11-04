using Hudoor.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hudoor.Api.Data.Configurations;

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("Sessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.Status)
            .HasMaxLength(50)
            .IsRequired()
            .IsUnicode(false);

        builder.Property(s => s.QrSecuritySalt)
            .HasMaxLength(100)
            .IsUnicode(false);

        builder.Property(s => s.SessionDateUtc)
            .IsRequired();

        builder.Property(s => s.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(s => s.TeacherId);
        builder.HasIndex(s => s.GroupId);
        builder.HasIndex(s => s.SessionDateUtc);

        builder.HasOne(s => s.Teacher)
            .WithMany(t => t.Sessions)
            .HasForeignKey(s => s.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Group)
            .WithMany(g => g.Sessions)
            .HasForeignKey(s => s.GroupId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
