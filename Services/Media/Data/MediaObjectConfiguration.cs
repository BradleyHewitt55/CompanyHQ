using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CompanyHQ.Services.Media.Contracts.Models;
using CompanyHQ.Services.Users.Contracts.Models;

namespace CompanyHQ.Services.Media.Data;

internal sealed class MediaObjectConfiguration : IEntityTypeConfiguration<MediaObject>
{
    public void Configure(EntityTypeBuilder<MediaObject> builder)
    {
        builder.Property(media => media.Key)
            .HasMaxLength(512)
            .IsRequired();
        builder.HasIndex(media => media.Key).IsUnique();

        builder.Property(media => media.ContentType)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(media => media.SizeBytes).IsRequired();
        builder.Property(media => media.CleanupFailureCount)
            .HasDefaultValue(0)
            .IsRequired();

        // Text storage keeps an enum insertion from changing the meaning of existing rows.
        builder.Property(media => media.Status)
            .HasConversion<string>()
            .HasColumnType("text")
            .IsRequired();
        builder.Property(media => media.Scope)
            .HasConversion<string>()
            .HasColumnType("text")
            .IsRequired();

        // The uploader's history is the query used by ownership and account-cleanup flows.
        builder.HasIndex(media => new { media.OwnerUserId, media.CreatedAt });

        // Cleanup scans old pending and deleted objects without walking ready media.
        builder.HasIndex(media => new { media.Status, media.CreatedAt });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(media => media.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
