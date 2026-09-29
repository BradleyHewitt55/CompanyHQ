using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CompanyHQ.Services.Mcp.Contracts.Models;
using CompanyHQ.Services.Users.Contracts.Models;

namespace CompanyHQ.Services.Mcp.Data;

internal sealed class AgentKeyConfiguration : IEntityTypeConfiguration<AgentKey>
{
    public void Configure(EntityTypeBuilder<AgentKey> builder)
    {
        builder.Property(key => key.Name).HasMaxLength(60);

        // Hex SHA-256. Unique because every /mcp request looks a row up by this value.
        builder.Property(key => key.KeyHash).HasMaxLength(64);
        builder.HasIndex(key => key.KeyHash).IsUnique();

        builder.Property(key => key.Prefix).HasMaxLength(8);
        builder.Property(key => key.OAuthClientId).HasMaxLength(2048);
        builder.Property(key => key.OAuthResource).HasMaxLength(2048);
        builder.Property(key => key.IsOAuth).HasDefaultValue(false);
        builder.Property(key => key.Access).HasConversion<string>().HasMaxLength(16);
        builder.Property(key => key.McpDisabledTools)
            .HasColumnType("text[]")
            .HasDefaultValue(Array.Empty<string>());

        // The owner's key list, newest first, filtered to not-yet-revoked — a supported page.
        builder.HasIndex(key => new { key.UserId, key.RevokedAt });

        // No navigation property: Mcp reads the account through the User service's model, and a
        // two-way link would drag keys around with the account graph.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(key => key.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AccessRole>().WithMany().HasForeignKey(key => key.AccessRoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(key => key.RevokedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
