using CompanyHQ.ServiceCatalog;
using CompanyHQ.ServiceCatalog.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyHQ.Appointments.Extensions;

public static class AppointmentsCatalogExtensions
{
    /// <summary>Registers public metadata. The host explicitly allowlists this service in AddServiceCatalog.</summary>
    public static IServiceCollection AddAppointmentsServiceDescriptor(this IServiceCollection services,
        string? loadedProvider = null, Func<IServiceProvider, bool>? isConfigured = null) =>
        services.AddServiceDescriptor(new(new(
            "appointments", "Appointments", "Let customers and staff book appointments.",
            "Each account gets a diary for booking, moving and cancelling appointments, including repeating ones. Staff with diary access can see and book in every diary. Turning it off hides the diaries and their pages; existing bookings are kept.",
            Core: false, Dependencies: [],
            Providers: null,
            DefaultProvider: null, MenuPath: "/appointments", Capabilities: null, ReadPermission: "appointments.manage"),
            typeof(AppointmentsCatalogExtensions).Assembly, loadedProvider,
            isConfigured));
}
