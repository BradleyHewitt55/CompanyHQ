using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CompanyHQ.Faqs.Contracts.Models;

namespace CompanyHQ.Faqs.Data;

internal sealed class FaqEntryConfiguration : IEntityTypeConfiguration<FaqEntry>
{
    public void Configure(EntityTypeBuilder<FaqEntry> builder)
    {
        builder.Property(entry => entry.Key).HasMaxLength(160).IsRequired();
        builder.HasIndex(entry => entry.Key).IsUnique();
        builder.Property(entry => entry.Question).HasMaxLength(300).IsRequired();
        builder.Property(entry => entry.Answer).HasMaxLength(20_000).IsRequired();
        builder.Property(entry => entry.Category).HasMaxLength(100).IsRequired();
        builder.Property(entry => entry.SeoDescription).HasMaxLength(320).IsRequired();
        builder.HasIndex(entry => new { entry.IsDraft, entry.SortOrder, entry.PublishedAt });
    }
}
