using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CompanyHQ.Moderation.Contracts.Models;

namespace CompanyHQ.Moderation.Data;

internal sealed class ReportedContentSnapshotConfiguration : IEntityTypeConfiguration<ReportedContentSnapshot>
{
    public void Configure(EntityTypeBuilder<ReportedContentSnapshot> builder)
    {
        builder.Property(snapshot => snapshot.AuthorUserId);
        builder.Property(snapshot => snapshot.Body).HasMaxLength(50_000).IsRequired();

        builder.HasOne<ContentReport>()
            .WithMany()
            .HasForeignKey(snapshot => snapshot.ContentReportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(snapshot => new { snapshot.ContentReportId, snapshot.OccurredAt });
    }
}
