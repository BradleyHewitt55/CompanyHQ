using Web.Admin.Components;
using Web.Admin.Services;
using Services.Companies.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Web.Admin;

public static class AdminServiceCollectionExtensions
{
    public static IServiceCollection AddCompanyHQAdmin(this IServiceCollection services)
    {
        services.AddSingleton<IServiceCatalog, StubServiceCatalog>();
        services.AddSingleton<IAdminNavigationCatalog, EmptyAdminNavigationCatalog>();
        return services;
    }

    public static void RegisterDefaultAdminComponents(this ComponentRegistry registry)
    {
        registry.Register("dashboard", typeof(Dashboard));
        registry.Register("services", typeof(ServicesPage));
        registry.Register("model", typeof(ModelEditor));
        registry.Register("users", typeof(UsersPlaceholder));
    }
}
