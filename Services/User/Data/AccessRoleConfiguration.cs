using CompanyHQ.Users.Contracts.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CompanyHQ.Users.Data;

internal sealed class AccessRoleConfiguration : IEntityTypeConfiguration<AccessRole>
{
    public void Configure(EntityTypeBuilder<AccessRole> builder)
    {
        builder.Property(role => role.Name).HasMaxLength(80);
        builder.HasIndex(role => role.Name).IsUnique();
        builder.Property(role => role.Permissions).HasColumnType("text[]")
            .HasDefaultValue(Array.Empty<string>());
    }
}
