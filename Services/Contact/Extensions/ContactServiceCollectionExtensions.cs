using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

using CompanyHQ.Services.Contact.Services;

namespace CompanyHQ.Services.Contact.Extensions;

/// <summary>Registration entry point for the Contact service and its controller.</summary>
public static class ContactServiceCollectionExtensions
{
    /// <summary>
    /// Registers the public contact form. The host must also register <c>IContactAddressBook</c> and
    /// <c>IContactMailer</c>; startup validation fails naming whichever is missing.
    /// </summary>
    public static IServiceCollection AddContact(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IContactService, ContactService>();
        services.AddRateLimiter(rateLimiter => ContactRateLimit.AddPolicy(rateLimiter));

        // Controllers live in this assembly, so the host has to be told to look in it.
        services.AddControllers().AddApplicationPart(typeof(ContactServiceCollectionExtensions).Assembly);

        return services;
    }
}
