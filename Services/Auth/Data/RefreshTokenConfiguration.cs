using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CompanyHQ.Services.Auth.Contracts.Models;
using CompanyHQ.Services.Users.Contracts.Models;

namespace CompanyHQ.Services.Auth.Data;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        // Hex SHA-256. Unique because every refresh looks a row up by this value.
        builder.Property(token => token.TokenHash).HasMaxLength(64);
        builder.HasIndex(token => token.TokenHash).IsUnique();

        // Revoking every session for one account is a supported operation; without this it is a scan.
        builder.HasIndex(token => token.UserId);

        builder.Property(token => token.DeviceLabel).HasMaxLength(64);

        // No navigation property: the Auth service reads users through the User service's model, and
        // a two-way link would make the account graph drag sessions around with it.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
