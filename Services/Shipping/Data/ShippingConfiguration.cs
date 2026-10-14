using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CompanyHQ.Shipping.Contracts.Models;

namespace CompanyHQ.Shipping.Data;

internal sealed class ShippingMethodConfiguration : IEntityTypeConfiguration<ShippingMethod>
{
    public void Configure(EntityTypeBuilder<ShippingMethod> builder)
    {
        builder.Property(method => method.Name).HasMaxLength(255);
        builder.Property(method => method.Code).HasMaxLength(100);
        builder.Property(method => method.Description).HasMaxLength(1_000);
        builder.Property(method => method.Cost).HasPrecision(18, 2);
        builder.Property(method => method.MethodType).HasConversion<string>().HasMaxLength(32);

        builder.ToTable(table => table.HasCheckConstraint("ck_shipping_method_cost", "cost >= 0"));

        // Every quote reads the active methods, cheapest first.
        builder.HasIndex(method => new { method.IsActive, method.Cost });
    }
}

internal sealed class DeliveryZoneConfiguration : IEntityTypeConfiguration<DeliveryZone>
{
    public void Configure(EntityTypeBuilder<DeliveryZone> builder)
    {
        builder.Property(zone => zone.Name).HasMaxLength(255);
        builder.Property(zone => zone.Description).HasMaxLength(1_000);

        builder.HasIndex(zone => zone.ShippingMethodId);
        builder.HasOne<ShippingMethod>()
            .WithMany()
            .HasForeignKey(zone => zone.ShippingMethodId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class DeliveryZonePostalCodeRuleConfiguration : IEntityTypeConfiguration<DeliveryZonePostalCodeRule>
{
    public void Configure(EntityTypeBuilder<DeliveryZonePostalCodeRule> builder)
    {
        builder.Property(rule => rule.Pattern).HasMaxLength(64);
        builder.Property(rule => rule.RuleType).HasConversion<string>().HasMaxLength(16);

        builder.HasIndex(rule => rule.DeliveryZoneId);
        builder.HasOne<DeliveryZone>()
            .WithMany()
            .HasForeignKey(rule => rule.DeliveryZoneId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class FulfillmentOriginConfiguration : IEntityTypeConfiguration<FulfillmentOrigin>
{
    public void Configure(EntityTypeBuilder<FulfillmentOrigin> builder)
    {
        builder.Property(origin => origin.Name).HasMaxLength(255);
        builder.Property(origin => origin.StreetAddress).HasMaxLength(255);
        builder.Property(origin => origin.ApartmentUnit).HasMaxLength(100);
        builder.Property(origin => origin.Suburb).HasMaxLength(150);
        builder.Property(origin => origin.City).HasMaxLength(150);
        builder.Property(origin => origin.Province).HasMaxLength(150);
        builder.Property(origin => origin.PostalCode).HasMaxLength(32);
        builder.Property(origin => origin.Country).HasMaxLength(100);
    }
}

internal sealed class ShippingMethodFulfillmentOriginConfiguration : IEntityTypeConfiguration<ShippingMethodFulfillmentOrigin>
{
    public void Configure(EntityTypeBuilder<ShippingMethodFulfillmentOrigin> builder)
    {
        builder.HasIndex(link => new { link.ShippingMethodId, link.FulfillmentOriginId }).IsUnique();

        builder.HasOne<ShippingMethod>()
            .WithMany()
            .HasForeignKey(link => link.ShippingMethodId)
            .OnDelete(DeleteBehavior.Cascade);

        // Removing an origin unlinks it from every method rather than blocking the delete.
        builder.HasOne<FulfillmentOrigin>()
            .WithMany()
            .HasForeignKey(link => link.FulfillmentOriginId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
