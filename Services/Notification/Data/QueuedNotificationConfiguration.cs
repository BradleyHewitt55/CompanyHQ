using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CompanyHQ.Notifications.Contracts.Models;
using CompanyHQ.Users.Contracts.Models;

namespace CompanyHQ.Notifications.Data;

internal sealed class QueuedNotificationConfiguration : IEntityTypeConfiguration<QueuedNotification>
{
    public void Configure(EntityTypeBuilder<QueuedNotification> builder)
    {
        builder.Property(notification => notification.Kind).HasMaxLength(128).IsRequired();
        builder.Property(notification => notification.Title).HasMaxLength(255).IsRequired();
        builder.Property(notification => notification.Body).HasMaxLength(4000).IsRequired();
        builder.Property(notification => notification.DataJson).HasColumnType("jsonb");
        builder.Property(notification => notification.Channels).HasConversion<string>().HasMaxLength(16);
        builder.Property(notification => notification.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(notification => notification.LastError).HasMaxLength(1000);
        builder.HasIndex(notification => new
        {
            notification.Status,
            notification.NextAttemptAt
        });
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(notification => notification.RecipientUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
