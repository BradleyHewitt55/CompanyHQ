using CompanyHQ.ServiceCatalog;
using CompanyHQ.ServiceCatalog.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyHQ.Sales.Extensions;

public static class SalesCatalogExtensions
{
    /// <summary>Registers public metadata. The host explicitly allowlists this service in AddServiceCatalog.</summary>
    public static IServiceCollection AddSalesServiceDescriptor(this IServiceCollection services,
        string? loadedProvider = null, Func<IServiceProvider, bool>? isConfigured = null)
    {
        var availablePorts = new Dictionary<string, bool>
        {
            ["shipping"] = services.Any(item => item.ServiceType == typeof(CompanyHQ.Sales.Contracts.Ports.ISalesShipping)),
            ["documents"] = services.Any(item => item.ServiceType == typeof(CompanyHQ.Sales.Contracts.Ports.ISalesDocumentDelivery)),
        };
        return
        services.AddServiceDescriptor(new(new(
            "sales", "Sales", "Sell products online.",
            "Products with options and variants, prices calculated from cost and markup, coupons and bundles, a shopping cart, orders, quotes, invoices and post-order upsells. Staff see costs and margins; customers only see selling prices. Uses Shipping for delivery quotes.",
            Core: false, Dependencies: [],
            Providers: null,
            DefaultProvider: null, MenuPath: "/sales", Capabilities: [new("shipping", "Delivery quotes through Shipping.", ["shipping"]), new("documents", "Queued document delivery through Email.", ["email"])], ReadPermission: "sales.manage",
            ConfigurationKeys: [
                new("Sales:BusinessTimeZone", "Business Time Zone.", Required: false),
                new("Sales:DefaultCurrency", "Default Currency.", Required: false),
                new("Sales:InvoicePrefix", "Invoice Prefix.", Required: false),
                new("Sales:PreferredProfitShippingMethodCode", "Preferred Profit Shipping Method Code.", Required: false),
                new("Sales:QuotePrefix", "Quote Prefix.", Required: false),
                new("Sales:GuestTokenLifetimeDays", "Guest Token Lifetime Days.", Required: false),
                new("Sales:RequireShippingAddress", "Require Shipping Address.", Required: false),
                new("Sales:EnableAuthenticatedCheckout", "Enable Authenticated Checkout.", Required: false),
                new("Sales:EnableExtraLengthPricing", "Enable Extra Length Pricing.", Required: false),
                new("Sales:ExtraLengthOptionName", "Extra Length Option Name.", Required: false),
                new("Sales:ExtraLengthValueContains", "Extra Length Value Contains.", Required: false),
                new("Sales:StandardLengthValueContains", "Standard Length Value Contains.", Required: false),
                new("Sales:ExtraLengthMultiplier", "Extra Length Multiplier.", Required: false)
            ]),
            typeof(SalesCatalogExtensions).Assembly, loadedProvider,
            isConfigured, (_, capability, _) => Task.FromResult(availablePorts.GetValueOrDefault(capability))));
    }
}
