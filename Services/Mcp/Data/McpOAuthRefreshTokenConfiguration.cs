using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CompanyHQ.Services.Mcp.Contracts.Models;
using CompanyHQ.Services.Users.Contracts.Models;

namespace CompanyHQ.Services.Mcp.Data;

internal sealed class McpOAuthRefreshTokenConfiguration : IEntityTypeConfiguration<McpOAuthRefreshToken>
{
    public void Configure(EntityTypeBuilder<McpOAuthRefreshToken> builder)
    {
        builder.Property(token => token.TokenHash).HasMaxLength(64);
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.Property(token => token.ClientId).HasMaxLength(2048);
        builder.Property(token => token.Resource).HasMaxLength(2048);
        builder.HasIndex(token => new { token.UserId, token.RevokedAt });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
