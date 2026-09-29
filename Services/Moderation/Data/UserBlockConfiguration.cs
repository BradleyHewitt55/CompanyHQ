using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CompanyHQ.Services.Moderation.Contracts.Models;
using CompanyHQ.Services.Users.Contracts.Models;

namespace CompanyHQ.Services.Moderation.Data;

internal sealed class UserBlockConfiguration : IEntityTypeConfiguration<UserBlock>
{
    public void Configure(EntityTypeBuilder<UserBlock> builder)
    {
        builder.Property(block => block.Reason).HasMaxLength(500);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(block => block.BlockerUserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(block => block.BlockedUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(block => new { block.BlockerUserId, block.BlockedUserId })
            .IsUnique();
        builder.HasIndex(block => block.BlockedUserId);
        builder.ToTable(table => table.HasCheckConstraint(
            "ck_user_block_distinct_users",
            "blocker_user_id <> blocked_user_id"));
    }
}
