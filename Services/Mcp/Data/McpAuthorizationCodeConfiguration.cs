using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CompanyHQ.Services.Mcp.Contracts.Models;
using CompanyHQ.Services.Users.Contracts.Models;

namespace CompanyHQ.Services.Mcp.Data;

internal sealed class McpAuthorizationCodeConfiguration : IEntityTypeConfiguration<McpAuthorizationCode>
{
    public void Configure(EntityTypeBuilder<McpAuthorizationCode> builder)
    {
        builder.Property(code => code.CodeHash).HasMaxLength(64);
        builder.HasIndex(code => code.CodeHash).IsUnique();

        builder.Property(code => code.ClientId).HasMaxLength(2048);
        builder.Property(code => code.RedirectUri).HasMaxLength(2048);
        builder.Property(code => code.Resource).HasMaxLength(2048);
        builder.Property(code => code.CodeChallenge).HasMaxLength(128);
        builder.HasIndex(code => new { code.UserId, code.ExpiresAt });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(code => code.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
