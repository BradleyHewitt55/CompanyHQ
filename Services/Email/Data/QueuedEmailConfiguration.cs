using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CompanyHQ.Email.Contracts.Models;

namespace CompanyHQ.Email.Data;

internal sealed class QueuedEmailConfiguration : IEntityTypeConfiguration<QueuedEmail>
{
    public void Configure(EntityTypeBuilder<QueuedEmail> builder)
    {
        builder.Property(email => email.To).IsRequired();
        builder.Property(email => email.Subject).HasMaxLength(255).IsRequired();
        builder.Property(email => email.Html).HasColumnType("text").IsRequired();
        builder.Property(email => email.Text).HasColumnType("text").IsRequired();
        builder.Property(email => email.IdempotencyKey).HasMaxLength(256).IsRequired();
        builder.HasIndex(email => email.IdempotencyKey).IsUnique();
        builder.Property(email => email.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(email => email.ProviderMessageId).HasMaxLength(128);
        builder.Property(email => email.LastError).HasMaxLength(1000);
        builder.HasIndex(email => new
        {
            email.Status,
            email.NextAttemptAt
        });
    }
}
