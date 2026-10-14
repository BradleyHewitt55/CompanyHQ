using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CompanyHQ.Notifications.Contracts.Models;
using CompanyHQ.Users.Contracts.Models;

namespace CompanyHQ.Notifications.Data;

internal sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.HasIndex(preference => preference.UserId).IsUnique();
        builder.Property(preference => preference.EmailEnabled).HasDefaultValue(true);
        builder.Property(preference => preference.PushEnabled).HasDefaultValue(true);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(preference => preference.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
