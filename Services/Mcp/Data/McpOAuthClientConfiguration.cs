using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CompanyHQ.Services.Mcp.Contracts.Models;

namespace CompanyHQ.Services.Mcp.Data;

internal sealed class McpOAuthClientConfiguration : IEntityTypeConfiguration<McpOAuthClient>
{
    public void Configure(EntityTypeBuilder<McpOAuthClient> builder)
    {
        builder.Property(client => client.ClientId).HasMaxLength(2048);
        builder.HasIndex(client => client.ClientId).IsUnique();
        builder.Property(client => client.RedirectUris)
            .HasColumnType("text[]")
            .IsRequired();
        builder.Property(client => client.GrantTypes)
            .HasColumnType("text[]")
            .IsRequired();
        builder.Property(client => client.ClientName).HasMaxLength(200);
    }
}
