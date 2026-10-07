using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;
using CompanyHQ.Services.Company.Contracts.Dtos;
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
        builder.Property(profile => profile.PublicEmails)
            .HasColumnType("jsonb")
            .HasConversion(
                emails => JsonSerializer.Serialize(emails, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<ClientCompanyEmailDto[]>(json, (JsonSerializerOptions?)null))
            .Metadata.SetValueComparer(new ValueComparer<ClientCompanyEmailDto[]?>(
                (left, right) => left == right || (left != null && right != null && left.SequenceEqual(right)),
                emails => emails == null ? 0 : emails.Aggregate(0, (hash, email) => HashCode.Combine(hash, email.GetHashCode())),
                emails => emails == null ? null : emails.ToArray()));
        builder.Property(profile => profile.LogoUrl).HasMaxLength(2048);
        builder.Property(profile => profile.Description).HasMaxLength(2_000).IsRequired();
        builder.Property(profile => profile.FacebookAccount).HasMaxLength(160);
        builder.Property(profile => profile.InstagramAccount).HasMaxLength(30);
        builder.Property(profile => profile.WhatsAppNumber).HasMaxLength(16);
    }
}
