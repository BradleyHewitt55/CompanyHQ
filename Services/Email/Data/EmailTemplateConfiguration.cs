using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CompanyHQ.Services.Email.Contracts.Models;

namespace CompanyHQ.Services.Email.Data;

internal sealed class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
{
    public void Configure(EntityTypeBuilder<EmailTemplate> builder)
    {
        builder.Property(template => template.Key).HasMaxLength(160).IsRequired();
        builder.HasIndex(template => template.Key).IsUnique();
        builder.Property(template => template.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(template => template.Html).HasColumnType("text").IsRequired();
    }
}
