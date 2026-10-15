using CompanyHQ.Company.Contracts.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CompanyHQ.Company.Data;

internal sealed class CompanyServiceStateConfiguration : IEntityTypeConfiguration<CompanyServiceState>
{
    public void Configure(EntityTypeBuilder<CompanyServiceState> builder) =>
        builder.Property(state => state.SelectionsJson).HasColumnType("jsonb").IsRequired();
}
