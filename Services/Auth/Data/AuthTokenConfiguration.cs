using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CompanyHQ.Services.Auth.Contracts.Models;
using CompanyHQ.Services.Users.Contracts.Models;

namespace CompanyHQ.Services.Auth.Data;

internal sealed class AuthTokenConfiguration : IEntityTypeConfiguration<AuthToken>
{
    public void Configure(EntityTypeBuilder<AuthToken> builder)
    {
        builder.Property(token => token.Purpose).HasConversion<string>().HasMaxLength(32);

        // Hex SHA-256. Unique because every consume looks a row up by this value.
        builder.Property(token => token.TokenHash).HasMaxLength(64);
        builder.HasIndex(token => token.TokenHash).IsUnique();

        // Invalidating every open token of a purpose for a user is a supported operation; without
        // this it is a scan.
        builder.HasIndex(token => new { token.UserId, token.Purpose });

        builder.Property(token => token.PendingEmail).HasMaxLength(320);

        // No navigation property, same reasoning as RefreshToken: Auth reads users through the User
        // service's model, and a two-way link would drag tokens around with the account graph.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
