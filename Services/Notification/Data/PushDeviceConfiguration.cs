using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CompanyHQ.Notifications.Contracts.Models;
using CompanyHQ.Users.Contracts.Models;

namespace CompanyHQ.Notifications.Data;

internal sealed class PushDeviceConfiguration : IEntityTypeConfiguration<PushDevice>
{
    public void Configure(EntityTypeBuilder<PushDevice> builder)
    {
        builder.Property(device => device.Token).HasMaxLength(4096).IsRequired();
        builder.HasIndex(device => device.Token).IsUnique();
        builder.Property(device => device.Platform).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(device => device.UserId).HasFilter("disabled_at IS NULL");
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(device => device.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
