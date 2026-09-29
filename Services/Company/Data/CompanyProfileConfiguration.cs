using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using CompanyHQ.Services.Company.Contracts.Models;

namespace CompanyHQ.Services.Company.Data;

internal sealed class CompanyProfileConfiguration : IEntityTypeConfiguration<CompanyProfile>
{
    public void Configure(EntityTypeBuilder<CompanyProfile> builder)
    {
        builder.Property(profile => profile.Name).HasMaxLength(160).IsRequired();
        builder.Property(profile => profile.Website).HasMaxLength(320).IsRequired();
        builder.Property(profile => profile.SupportEmail).HasMaxLength(320).IsRequired();
        builder.Property(profile => profile.InfoEmail).HasMaxLength(320).IsRequired();
        builder.Property(profile => profile.LogoUrl).HasMaxLength(2048);
        builder.Property(profile => profile.Description).HasMaxLength(2_000).IsRequired();
    }
}
