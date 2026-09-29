using System.Linq.Expressions;

using CompanyHQ.Services.Appointments.Contracts.Models;

namespace CompanyHQ.Services.Appointments.Contracts.Dtos;

/// <summary>The only place an <see cref="Appointment" /> becomes a wire DTO.</summary>
public static class AppointmentProjections
{
    public static readonly Expression<Func<Appointment, ClientAppointmentDto>> ToDto =
        appointment => new ClientAppointmentDto(
            appointment.Id,
            appointment.Title,
            appointment.ScheduledAt,
            appointment.Location,
            appointment.Notes,
            appointment.SourceKind,
            appointment.SourceId,
            appointment.CreatedAt,
            appointment.UpdatedAt,
            appointment.SourceKind == null);
}
