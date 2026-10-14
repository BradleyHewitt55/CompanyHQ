using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CompanyHQ.Moderation.Contracts.Enums;
using CompanyHQ.Moderation.Contracts.Models;
using CompanyHQ.Users.Contracts.Models;

namespace CompanyHQ.Moderation.Data;

internal sealed class AccountFlagConfiguration : IEntityTypeConfiguration<AccountFlag>
{
    public void Configure(EntityTypeBuilder<AccountFlag> builder)
    {
        builder.Property(flag => flag.Kind).HasMaxLength(128).IsRequired();
        builder.Property(flag => flag.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(AccountFlagStatus.Active)
            .IsRequired();
        builder.Property(flag => flag.RaisedBy)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(flag => flag.Evidence).HasMaxLength(4000).IsRequired();
        builder.Property(flag => flag.ReviewNote).HasMaxLength(2000);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(flag => flag.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Only one live flag of a kind may exist; cleared and upheld history remains queryable.
        builder.HasIndex(flag => new { flag.UserId, flag.Kind })
            .IsUnique()
            .HasFilter("status = 'Active'");
        builder.HasIndex(flag => new { flag.Status, flag.CreatedAt });
    }
}
