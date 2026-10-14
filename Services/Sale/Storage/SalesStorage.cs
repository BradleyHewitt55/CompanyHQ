using System.Text.Json;
using System.Text.Json.Serialization;
using CompanyHQ.Sales.Contracts.Dtos;
using CompanyHQ.Sales.Contracts.Forms;
using CompanyHQ.Sales.Contracts.Ports;
using CompanyHQ.Sales.Services;
namespace CompanyHQ.Sales.Storage;

/// <summary>Versioned persistence boundary. HTTP types are converted into independent frozen storage schemas.</summary>
internal static class SalesStorage
{
    private static readonly JsonSerializerOptions Format=new(JsonSerializerDefaults.Web);
    // Mapping accepts textual HTTP enums while frozen persisted V1 records remain numeric.
    private static readonly JsonSerializerOptions Mapping=new(JsonSerializerDefaults.Web)
        {Converters={new JsonStringEnumConverter()}};
    private sealed record Envelope(int Version,string Kind,JsonElement Data);
    private static T Map<T>(object source)=>JsonSerializer.SerializeToElement(source,source.GetType(),Mapping).Deserialize<T>(Mapping)!;
    public static string Write<T>(T source)
    {
        if(source is null)throw new ArgumentNullException(nameof(source));
        var (kind,data)=source switch
        {
            SaveProductRequest value => ("catalog.definition",JsonSerializer.SerializeToElement(Map<CatalogDefinitionV1>(value),Format)),
            SaveCouponRequest value => ("coupon.definition",JsonSerializer.SerializeToElement(Map<CouponDefinitionV1>(value),Format)),
            SaveUpsellRequest value => ("upsell.definition",JsonSerializer.SerializeToElement(Map<UpsellDefinitionV1>(value),Format)),
            SavePaymentMethodRequest value => ("payment.definition",JsonSerializer.SerializeToElement(Map<PaymentDefinitionV1>(value),Format)),
            SalesAddressDto value => ("address.snapshot",JsonSerializer.SerializeToElement(Map<AddressV1>(value),Format)),
            PlaceOrderRequest value => ("order.request",JsonSerializer.SerializeToElement(Map<CustomerOrderV1>(value),Format)),
            PricedBasket value => ("basket.snapshot",JsonSerializer.SerializeToElement(Map<BasketV1>(value),Format)),
            PreparedOrder value => ("document.prepared",JsonSerializer.SerializeToElement(Map<PreparedDocumentV1>(value),Format)),
            SalesLineRequest value => ("cart.line",JsonSerializer.SerializeToElement(Map<CartLineV1>(value),Format)),
            AdminSalesLineDto value => ("line.snapshot",JsonSerializer.SerializeToElement(Map<CostedLineV1>(value),Format)),
            UpsellAcceptanceDto value => ("upsell.receipt",JsonSerializer.SerializeToElement(Map<UpsellReceiptV1>(value),Format)),
            SalesShippingQuote value => ("shipping.snapshot",JsonSerializer.SerializeToElement(Map<ShippingQuoteV1>(value),Format)),
            SalesDocument value => ("delivery.document",JsonSerializer.SerializeToElement(Map<DeliveryDocumentV1>(value),Format)),
            IEnumerable<SalesAppliedCouponDto> values => ("coupon.snapshots",JsonSerializer.SerializeToElement(values.Select(x=>Map<CouponSnapshotV1>(x)).ToArray(),Format)),
            _=>throw new InvalidOperationException("A durable Sales schema is required for this type.")
        };
        return JsonSerializer.Serialize(new Envelope(1,kind,data),Format);
    }
    public static T Read<T>(string json)
    {
        var envelope=JsonSerializer.Deserialize<Envelope>(json,Format) ?? throw new InvalidOperationException("Missing storage envelope.");
        if(envelope.Version!=1)throw new InvalidOperationException($"Unsupported Sales storage version {envelope.Version}.");
        object stored=envelope.Kind switch
        {
            "catalog.definition" => envelope.Data.Deserialize<CatalogDefinitionV1>(Format)!,
            "coupon.definition" => envelope.Data.Deserialize<CouponDefinitionV1>(Format)!,
            "upsell.definition" => envelope.Data.Deserialize<UpsellDefinitionV1>(Format)!,
            "payment.definition" => envelope.Data.Deserialize<PaymentDefinitionV1>(Format)!,
            "address.snapshot" => envelope.Data.Deserialize<AddressV1>(Format)!,
            "order.request" => envelope.Data.Deserialize<CustomerOrderV1>(Format)!,
            "basket.snapshot" => envelope.Data.Deserialize<BasketV1>(Format)!,
            "document.prepared" => envelope.Data.Deserialize<PreparedDocumentV1>(Format)!,
            "cart.line" => envelope.Data.Deserialize<CartLineV1>(Format)!,
            "line.snapshot" => envelope.Data.Deserialize<CostedLineV1>(Format)!,
            "upsell.receipt" => envelope.Data.Deserialize<UpsellReceiptV1>(Format)!,
            "shipping.snapshot" => envelope.Data.Deserialize<ShippingQuoteV1>(Format)!,
            "delivery.document" => envelope.Data.Deserialize<DeliveryDocumentV1>(Format)!,
            "coupon.snapshots" => envelope.Data.Deserialize<CouponSnapshotV1[]>(Format)!,
            _=>throw new InvalidOperationException("Unknown Sales storage kind.")
        };
        var expected=typeof(T)==typeof(SalesAppliedCouponDto[])?"coupon.snapshots":typeof(T) switch
        {
            var t when t==typeof(SaveProductRequest) => "catalog.definition",
            var t when t==typeof(SaveCouponRequest) => "coupon.definition",
            var t when t==typeof(SaveUpsellRequest) => "upsell.definition",
            var t when t==typeof(SavePaymentMethodRequest) => "payment.definition",
            var t when t==typeof(SalesAddressDto) => "address.snapshot",
            var t when t==typeof(PlaceOrderRequest) => "order.request",
            var t when t==typeof(PricedBasket) => "basket.snapshot",
            var t when t==typeof(PreparedOrder) => "document.prepared",
            var t when t==typeof(SalesLineRequest) => "cart.line",
            var t when t==typeof(AdminSalesLineDto) => "line.snapshot",
            var t when t==typeof(UpsellAcceptanceDto) => "upsell.receipt",
            var t when t==typeof(SalesShippingQuote) => "shipping.snapshot",
            var t when t==typeof(SalesDocument) => "delivery.document",
            _=>throw new InvalidOperationException("No durable reader for this Sales type.")
        };
        if(expected!=envelope.Kind)throw new InvalidOperationException("Sales storage kind does not match the reader.");
        return Map<T>(stored);
    }
}
