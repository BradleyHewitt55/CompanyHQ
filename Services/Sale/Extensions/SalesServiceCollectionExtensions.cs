using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CompanyHQ.Services.Sales.Contracts.Models;
using CompanyHQ.Services.Sales.Services;
namespace CompanyHQ.Services.Sales.Extensions;
/// <summary>Registers generic catalog, server pricing and pending unpaid checkout.</summary>
public static class SalesServiceCollectionExtensions
{
    public static IServiceCollection AddSales(this IServiceCollection services,IConfiguration configuration)
    {
        var options=configuration.GetSection(SalesOptions.SectionName).Get<SalesOptions>() ?? new();
        if(options.GuestTokenLifetimeDays is <1 or >365)throw new ArgumentException("Sales guest token lifetime must be 1 to 365 days.");
        TimeZoneInfo.FindSystemTimeZoneById(options.BusinessTimeZone);
        if(!System.Text.RegularExpressions.Regex.IsMatch(options.DefaultCurrency,"^[A-Z]{3}$") ||
            string.IsNullOrWhiteSpace(options.InvoicePrefix) || options.InvoicePrefix.Length>20 ||
            string.IsNullOrWhiteSpace(options.QuotePrefix) || options.QuotePrefix.Length>20 ||
            options.EnableExtraLengthPricing && (string.IsNullOrWhiteSpace(options.ExtraLengthOptionName) || string.IsNullOrWhiteSpace(options.ExtraLengthValueContains) || string.IsNullOrWhiteSpace(options.StandardLengthValueContains) || options.ExtraLengthMultiplier is <1 or >10))
            throw new ArgumentException("Invalid Sales currency, document prefix or extra-length policy.");
        services.AddSingleton(options);
        services.AddHostedService<SalesCatalogPriceWorker>();
        if(options.EnableAuthenticatedCheckout)services.AddScoped<SalesCustomerResolution>();
        services.AddScoped<SalesProfitScenarioService>();services.AddScoped<SalesDocumentService>();services.AddScoped<SalesQuoteService>();services.AddScoped<SalesCatalogService>();services.AddScoped<SalesPromotionService>();services.AddScoped<SalesBasketPricing>();services.AddScoped<SalesCheckoutService>();services.AddScoped<SalesOrderService>();
        services.AddScoped<ISalesPromotionService>(sp=>sp.GetRequiredService<SalesPromotionService>());
        services.AddScoped<ISalesQuoteService>(sp=>sp.GetRequiredService<SalesQuoteService>());
        services.AddScoped<ISalesBasketPricing>(sp=>sp.GetRequiredService<SalesBasketPricing>());
        services.AddScoped<ISalesCatalogService>(sp=>sp.GetRequiredService<SalesCatalogService>());
        services.AddScoped<ISalesCheckoutService>(sp=>sp.GetRequiredService<SalesCheckoutService>());
        services.AddScoped<ISalesOrderService>(sp=>sp.GetRequiredService<SalesOrderService>());
        services.AddControllers().AddApplicationPart(typeof(SalesServiceCollectionExtensions).Assembly);
        services.AddRateLimiter(o=>o.AddPolicy("sales.public",context=>RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=60,Window=TimeSpan.FromMinutes(1),QueueLimit=0})));
        return services;
    }
}
