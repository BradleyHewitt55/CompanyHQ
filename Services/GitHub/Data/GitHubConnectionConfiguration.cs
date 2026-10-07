using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CompanyHQ.Services.GitHub.Contracts.Models;

namespace CompanyHQ.Services.GitHub.Data;

internal sealed class GitHubConnectionConfiguration : IEntityTypeConfiguration<GitHubConnection>
{
    public void Configure(EntityTypeBuilder<GitHubConnection> builder)
    {
        builder.Property(connection => connection.Repository).HasMaxLength(320).IsRequired();
        builder.Property(connection => connection.TokenEncrypted).HasMaxLength(4096).IsRequired();
    }
}
