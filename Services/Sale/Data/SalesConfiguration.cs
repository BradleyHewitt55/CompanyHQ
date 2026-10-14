using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CompanyHQ.Sales.Contracts.Models;
namespace CompanyHQ.Sales.Data;
internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b) { b.ToTable("sales_product"); b.Property(x=>x.Name).HasMaxLength(255); b.Property(x=>x.Slug).HasMaxLength(255); b.HasIndex(x=>x.Slug).IsUnique(); b.HasIndex(x=>new{x.IsVisible,x.Name}); b.Property(x=>x.DefinitionJson).HasColumnType("jsonb"); b.Property(x=>x.EffectivePrice).HasPrecision(18,2);b.Property(x=>x.AverageRating).HasPrecision(5,2);b.HasIndex(x=>x.EffectivePrice);b.HasIndex(x=>x.AverageRating); }
}
internal sealed class VariantConfiguration : IEntityTypeConfiguration<CatalogVariant>
{
    public void Configure(EntityTypeBuilder<CatalogVariant> b) { b.ToTable("sales_variant"); b.Property(x=>x.EffectivePrice).HasPrecision(18,2); b.Property(x=>x.Sku).HasMaxLength(100); b.HasIndex(x=>new{x.ProductId,x.Sku}).IsUnique(); b.HasOne<Product>().WithMany().HasForeignKey(x=>x.ProductId).OnDelete(DeleteBehavior.Cascade); }
}
internal sealed class SelectionConfiguration : IEntityTypeConfiguration<VariantSelection>
{
    public void Configure(EntityTypeBuilder<VariantSelection> b) { b.ToTable("sales_variant_selection"); b.Property(x=>x.Name).HasMaxLength(100); b.Property(x=>x.Value).HasMaxLength(255); b.HasIndex(x=>new{x.VariantId,x.Name}).IsUnique(); b.HasIndex(x=>new{x.Name,x.Value}); b.HasOne<CatalogVariant>().WithMany().HasForeignKey(x=>x.VariantId).OnDelete(DeleteBehavior.Cascade); }
}
internal sealed class BundleLinkConfiguration : IEntityTypeConfiguration<BundleLink>
{
    public void Configure(EntityTypeBuilder<BundleLink> b) { b.ToTable("sales_bundle_link"); b.HasIndex(x=>new{x.ProductId,x.ComponentProductId}).IsUnique(); b.HasOne<Product>().WithMany().HasForeignKey(x=>x.ProductId).OnDelete(DeleteBehavior.Cascade); b.HasOne<Product>().WithMany().HasForeignKey(x=>x.ComponentProductId).OnDelete(DeleteBehavior.Restrict); }
}
internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b) { b.ToTable("sales_category"); b.Property(x=>x.Sku).HasMaxLength(100);b.Property(x=>x.ImageUrl).HasMaxLength(2000);b.HasOne<Category>().WithMany().HasForeignKey(x=>x.ParentId).OnDelete(DeleteBehavior.Restrict); b.Property(x=>x.Name).HasMaxLength(255); b.Property(x=>x.Slug).HasMaxLength(255); b.HasIndex(x=>x.Slug).IsUnique(); }
}
internal sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> b) { b.ToTable("sales_supplier"); b.Property(x=>x.Name).HasMaxLength(255); b.Property(x=>x.OriginJson).HasColumnType("jsonb"); }
}
internal sealed class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> b) { b.ToTable("sales_coupon"); b.Property(x=>x.Code).HasMaxLength(100); b.HasIndex(x=>x.Code).IsUnique(); b.Property(x=>x.DefinitionJson).HasColumnType("jsonb"); }
}
internal sealed class UpsellConfiguration : IEntityTypeConfiguration<UpsellOffer>
{
    public void Configure(EntityTypeBuilder<UpsellOffer> b) { b.ToTable("sales_upsell_offer"); b.HasIndex(x=>new{x.ParentProductId,x.ProductId}).IsUnique(); b.Property(x=>x.DefinitionJson).HasColumnType("jsonb"); b.HasOne<Product>().WithMany().HasForeignKey(x=>x.ParentProductId).OnDelete(DeleteBehavior.Cascade); b.HasOne<Product>().WithMany().HasForeignKey(x=>x.ProductId).OnDelete(DeleteBehavior.Cascade); }
}
internal sealed class SettingsConfiguration : IEntityTypeConfiguration<SalesSettings>
{
    public void Configure(EntityTypeBuilder<SalesSettings> b) { b.ToTable("sales_settings"); b.Property(x=>x.UpsellExtraDiscountPercent).HasPrecision(5,2); }
}
internal sealed class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> b) { b.ToTable("sales_payment_method"); b.Property(x=>x.Name).HasMaxLength(255); b.Property(x=>x.DefinitionJson).HasColumnType("jsonb"); }
}
internal sealed class CounterConfiguration : IEntityTypeConfiguration<SalesCounter>
{
    public void Configure(EntityTypeBuilder<SalesCounter> b) { b.ToTable("sales_counter"); }
}
internal sealed class OrderConfiguration : IEntityTypeConfiguration<SalesOrder>
{
    public void Configure(EntityTypeBuilder<SalesOrder> b) { b.ToTable("sales_order"); b.Property(x=>x.CustomerFirstName).HasMaxLength(100);b.Property(x=>x.CustomerLastName).HasMaxLength(100);b.Property(x=>x.CustomerEmail).HasMaxLength(255); b.HasIndex(x=>x.Number).IsUnique(); b.HasIndex(x=>x.InvoiceNumber).IsUnique(); b.Property(x=>x.InvoiceReference).HasMaxLength(40); b.Property(x=>x.ManualDiscountValue).HasPrecision(18,2); b.HasIndex(x=>new{x.UserId,x.CreatedAt}); b.Property(x=>x.GuestTokenHash).HasMaxLength(64); b.Property(x=>x.RequestJson).HasColumnType("jsonb"); b.Property(x=>x.SnapshotJson).HasColumnType("jsonb"); b.Property(x=>x.ShippingJson).HasColumnType("jsonb"); b.Property(x=>x.TotalAmount).HasPrecision(18,2); b.Property(x=>x.Currency).HasMaxLength(3); }
}
internal sealed class AcceptanceConfiguration : IEntityTypeConfiguration<OrderUpsellAcceptance>
{
    public void Configure(EntityTypeBuilder<OrderUpsellAcceptance> b) { b.ToTable("sales_order_upsell_acceptance"); b.HasIndex(x=>new{x.OrderId,x.RequestId}).IsUnique(); b.HasIndex(x=>new{x.OrderId,x.OfferId}).IsUnique(); b.Property(x=>x.ResultJson).HasColumnType("jsonb"); b.Property(x=>x.LineJson).HasColumnType("jsonb"); b.Property(x=>x.PricedLineJson).HasColumnType("jsonb"); b.Property(x=>x.CouponsJson).HasColumnType("jsonb"); b.Property(x=>x.ShippingJson).HasColumnType("jsonb"); b.HasOne<SalesOrder>().WithMany().HasForeignKey(x=>x.OrderId).OnDelete(DeleteBehavior.Restrict); }
}

internal sealed class QuoteConfiguration : IEntityTypeConfiguration<SalesQuote>
{
    public void Configure(EntityTypeBuilder<SalesQuote> b) { b.ToTable("sales_quote"); b.Property(x=>x.QuoteReference).HasMaxLength(40); b.HasIndex(x=>x.Number).IsUnique(); b.Property(x=>x.PreparedJson).HasColumnType("jsonb"); b.Property(x=>x.TotalAmount).HasPrecision(18,2); b.HasIndex(x=>x.ConvertedToOrderId).IsUnique(); b.Property(x=>x.CustomerFirstName).HasMaxLength(100); b.Property(x=>x.CustomerLastName).HasMaxLength(100); b.Property(x=>x.CustomerEmail).HasMaxLength(255); b.Property(x=>x.Currency).HasMaxLength(3); }
}

internal sealed class RevisionConfiguration : IEntityTypeConfiguration<SalesOrderRevision>
{
    public void Configure(EntityTypeBuilder<SalesOrderRevision> b) {b.ToTable("sales_order_revision");b.HasIndex(x=>new{x.OrderId,x.Number}).IsUnique();b.Property(x=>x.PreparedJson).HasColumnType("jsonb");b.HasOne<SalesOrder>().WithMany().HasForeignKey(x=>x.OrderId).OnDelete(DeleteBehavior.Restrict);}
}

internal sealed class ReviewConfiguration : IEntityTypeConfiguration<ProductReview>
{
    public void Configure(EntityTypeBuilder<ProductReview> b) {b.ToTable("sales_product_review");b.Property(x=>x.Title).HasMaxLength(255);b.Property(x=>x.Comment).HasMaxLength(2000);b.Property(x=>x.AuthorName).HasMaxLength(255);b.HasOne<Product>().WithMany().HasForeignKey(x=>x.ProductId).OnDelete(DeleteBehavior.Cascade);b.HasIndex(x=>new{x.ProductId,x.CreatedAt});b.ToTable(t=>t.HasCheckConstraint("ck_sales_review_rating","rating >= 1 AND rating <= 5"));}
}
internal sealed class InvoiceDeliveryConfiguration : IEntityTypeConfiguration<InvoiceDeliveryRequest>
{
    public void Configure(EntityTypeBuilder<InvoiceDeliveryRequest> b)
    {
        b.ToTable("sales_invoice_delivery_request");
        b.HasIndex(x=>new{x.OrderId,x.RequestId}).IsUnique();
        b.Property(x=>x.DocumentJson).HasColumnType("jsonb");
        b.HasOne<SalesOrder>().WithMany().HasForeignKey(x=>x.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class ProjectionConfiguration : IEntityTypeConfiguration<CatalogPriceProjection>
{
    public void Configure(EntityTypeBuilder<CatalogPriceProjection> b) {b.ToTable("sales_catalog_price_projection");b.Property(x=>x.PolicyKey).HasMaxLength(64);}
}
