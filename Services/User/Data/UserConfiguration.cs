using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using CompanyHQ.Users.Contracts.Models;

namespace CompanyHQ.Users.Data;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(user => user.Email).HasMaxLength(320);
        builder.HasIndex(user => user.Email).IsUnique();

        builder.Property(user => user.FirstName).HasMaxLength(60);
        builder.Property(user => user.LastName).HasMaxLength(60);
        builder.Property(user => user.Bio).HasMaxLength(500);
        builder.Property(user => user.ProfileImageUrl).HasMaxLength(2048);
        // /api/users/{slug} and the member search both match on the names, so the lookup needs an index.
        builder.HasIndex(user => new { user.FirstName, user.LastName });

        builder.Property(user => user.McpDisabledTools)
            .HasColumnType("text[]")
            .HasDefaultValue(Array.Empty<string>());

        // Stored as text so a new role does not silently reinterpret existing rows the way shifting
        // enum ordinals would.
        builder.Property(user => user.Role).HasConversion<string>().HasMaxLength(32);
        builder.HasOne(user => user.AccessRole).WithMany().HasForeignKey(user => user.AccessRoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
