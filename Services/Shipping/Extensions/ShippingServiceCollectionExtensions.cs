using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using CompanyHQ.Services.Shipping.Services;

namespace CompanyHQ.Services.Shipping.Extensions;

/// <summary>Registers the Shipping service and its controller assembly.</summary>
public static class ShippingServiceCollectionExtensions
{
    /// <summary>
    /// Registers shipping methods, origins and quotes. <c>IShippingCatalog</c>,
    /// <c>IShippingGeocoder</c> and <c>IShippingDiscountPolicy</c> are optional ports the host may
    /// fill; the service quotes without them.
    /// </summary>
    public static IServiceCollection AddShipping(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IShippingService, ShippingService>();
        services.AddScoped<ShippingAddressService>();
        services.AddRateLimiter(rateLimiter => ShippingRateLimit.AddPolicy(rateLimiter));
        services.AddControllers().AddApplicationPart(typeof(ShippingServiceCollectionExtensions).Assembly);

        return services;
    }
}
