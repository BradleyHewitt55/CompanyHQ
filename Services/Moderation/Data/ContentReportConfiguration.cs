using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CompanyHQ.Services.Moderation.Contracts.Enums;
using CompanyHQ.Services.Moderation.Contracts.Models;
using CompanyHQ.Services.Users.Contracts.Models;

namespace CompanyHQ.Services.Moderation.Data;

internal sealed class ContentReportConfiguration : IEntityTypeConfiguration<ContentReport>
{
    public void Configure(EntityTypeBuilder<ContentReport> builder)
    {
        builder.Property(report => report.TargetKind).HasMaxLength(32).IsRequired();
        builder.Property(report => report.Reason)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(report => report.Details).HasMaxLength(1000);
        builder.Property(report => report.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(ReportStatus.Pending)
            .IsRequired();
        builder.Property(report => report.ReviewNote).HasMaxLength(2000);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(report => report.ReporterUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(report => new
        {
            report.ReporterUserId,
            report.TargetKind,
            report.TargetId
        })
            .IsUnique()
            .HasFilter("status <> 'Withdrawn'");
        builder.HasIndex(report => new { report.Status, report.CreatedAt });
        builder.HasIndex(report => new { report.TargetKind, report.TargetId });
    }
}
