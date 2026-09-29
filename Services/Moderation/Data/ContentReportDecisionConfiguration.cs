using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CompanyHQ.Services.Moderation.Contracts.Enums;
using CompanyHQ.Services.Moderation.Contracts.Models;
using CompanyHQ.Services.Users.Contracts.Models;

namespace CompanyHQ.Services.Moderation.Data;

internal sealed class ContentReportDecisionConfiguration : IEntityTypeConfiguration<ContentReportDecision>
{
    public void Configure(EntityTypeBuilder<ContentReportDecision> builder)
    {
        builder.Property(decision => decision.PreviousStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(decision => decision.NewStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(decision => decision.Note).HasMaxLength(2000);
        builder.Property(decision => decision.DecidedAt).IsRequired();

        builder.HasOne<ContentReport>()
            .WithMany()
            .HasForeignKey(decision => decision.ContentReportId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(decision => decision.AdministratorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(decision => new { decision.ContentReportId, decision.DecidedAt });
    }
}
