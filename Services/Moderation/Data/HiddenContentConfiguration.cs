using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CompanyHQ.Moderation.Contracts.Enums;
using CompanyHQ.Moderation.Contracts.Models;
using CompanyHQ.Users.Contracts.Models;

namespace CompanyHQ.Moderation.Data;

internal sealed class HiddenContentConfiguration : IEntityTypeConfiguration<HiddenContent>
{
    public void Configure(EntityTypeBuilder<HiddenContent> builder)
    {
        builder.Property(hidden => hidden.TargetKind).HasMaxLength(32).IsRequired();
        builder.Property(hidden => hidden.Source)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(HiddenContentSource.Legacy)
            .IsRequired();
        builder.Property(hidden => hidden.SourceId).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(hidden => hidden.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(hidden => new
        {
            hidden.UserId,
            hidden.TargetKind,
            hidden.TargetId,
            hidden.Source,
            hidden.SourceId
        }).IsUnique();
        builder.HasIndex(hidden => new { hidden.UserId, hidden.TargetKind });
    }
}
