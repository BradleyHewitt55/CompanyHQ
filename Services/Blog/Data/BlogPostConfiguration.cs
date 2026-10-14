using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CompanyHQ.Blogs.Contracts.Models;

namespace CompanyHQ.Blogs.Data;

internal sealed class BlogPostConfiguration : IEntityTypeConfiguration<BlogPost>
{
    public void Configure(EntityTypeBuilder<BlogPost> builder)
    {
        builder.Property(post => post.Slug).HasMaxLength(160).IsRequired();
        builder.HasIndex(post => post.Slug).IsUnique();
        builder.Property(post => post.Title).HasMaxLength(240).IsRequired();
        builder.Property(post => post.Description).HasMaxLength(320).IsRequired();
        builder.Property(post => post.Body).HasMaxLength(100_000).IsRequired();
        builder.Property(post => post.Author).HasMaxLength(120).IsRequired();
        builder.Property(post => post.Category).HasMaxLength(100).IsRequired();
        builder.Property(post => post.CoverImageUrl).HasMaxLength(2048);
        builder.HasIndex(post => new { post.IsDraft, post.PublishedAt, post.SortOrder });
    }
}
