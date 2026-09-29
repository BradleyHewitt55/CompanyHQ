using Microsoft.Extensions.DependencyInjection;
using CompanyHQ.Services.Faqs.Services;

namespace CompanyHQ.Services.Faqs.Extensions;

/// <summary>Registers the FAQ content service and its controller assembly.</summary>
public static class FaqServiceCollectionExtensions
{
    public static IServiceCollection AddFaqs(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IFaqService, FaqService>();
        services.AddControllers().AddApplicationPart(typeof(FaqServiceCollectionExtensions).Assembly);

        return services;
    }
}
