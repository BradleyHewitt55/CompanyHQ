using CompanyHQ.ServiceCatalog;
using CompanyHQ.ServiceCatalog.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyHQ.Users.Extensions;

public static class UsersCatalogExtensions
{
    /// <summary>Registers public metadata. The host explicitly allowlists this service in AddServiceCatalog.</summary>
    public static IServiceCollection AddUsersServiceDescriptor(this IServiceCollection services,
        string? loadedProvider = null, Func<IServiceProvider, bool>? isConfigured = null) =>
        services.AddServiceDescriptor(new(new(
            "users", "Users", "Accounts, roles and permissions.",
            "Manage member and staff accounts and the roles that decide what each person may do. Always on.",
            Core: true, Dependencies: [],
            Providers: null,
            DefaultProvider: null, MenuPath: "/users", Capabilities: null, ReadPermission: "users.manage"),
            typeof(UsersCatalogExtensions).Assembly, loadedProvider,
            isConfigured));
}
