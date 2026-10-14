using Microsoft.Extensions.DependencyInjection;

using CompanyHQ.Appointments.Contracts.Ports;
using CompanyHQ.Appointments.Services;

namespace CompanyHQ.Appointments.Extensions;

/// <summary>Registers the Appointment service and its controller assembly.</summary>
public static class AppointmentServiceCollectionExtensions
{
    /// <summary>Registers the member appointment diary and its scheduling port.</summary>
    public static IServiceCollection AddAppointments(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IAppointmentService, AppointmentService>();

        // The port other services call. They reference Appointment.Contracts only; DI supplies this.
        services.AddScoped<IAppointmentScheduler, AppointmentScheduler>();
        services.AddControllers().AddApplicationPart(typeof(AppointmentServiceCollectionExtensions).Assembly);

        return services;
    }
}
