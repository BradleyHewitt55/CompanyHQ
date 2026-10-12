using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CompanyHQ.Services.Persistence.Services;
using CompanyHQ.Services.Users.Contracts.Models;
using CompanyHQ.Services.Users.Services;

namespace CompanyHQ.Services.Users.Extensions;

public static class UserServiceCollectionExtensions
{
    /// <summary>
    /// Registers password hashing and the admin seeder. Call after <c>AddPostgres</c>.
    /// </summary>
    public static IServiceCollection AddUsers(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(
            configuration.GetSection(AdminSeedOptions.SectionName).Get<AdminSeedOptions>() ?? new());
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IDataSeeder, AdminSeeder>();

        // The single deletion path. Guards are contributed by other services and may be absent, in
        // which case this simply deletes.
        services.AddScoped<IAccountDeletionCoordinator, AccountDeletionCoordinator>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserAuthorizationService, UserAuthorizationService>();
        services.AddScoped<IUserAdministrationService, UserAdministrationService>();
        services.AddScoped<IAccessRoleService, AccessRoleService>();
        services.AddScoped<IUserFavoritePlaceService, UserFavoritePlaceService>();

        // Controllers live in this assembly, so the host has to be told to look in it.
        services.AddControllers().AddApplicationPart(typeof(UserServiceCollectionExtensions).Assembly);

        return services;
    }
}
