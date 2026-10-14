using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CompanyHQ.Users.Contracts.Models;

namespace CompanyHQ.Users.Data;

internal sealed class UserFavoritePlaceConfiguration : IEntityTypeConfiguration<UserFavoritePlace>
{
    public void Configure(EntityTypeBuilder<UserFavoritePlace> builder)
    {
        builder.Property(place => place.Name).HasMaxLength(200);
        builder.Property(place => place.Address).HasMaxLength(500);
        builder.Property(place => place.Phone).HasMaxLength(50);
        builder.Property(place => place.Kind).HasMaxLength(64);
        builder.HasIndex(place => new { place.UserId, place.Kind, place.Latitude, place.Longitude, place.Name }).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(place => place.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
