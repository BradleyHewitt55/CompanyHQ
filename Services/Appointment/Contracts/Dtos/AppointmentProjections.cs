using System.Linq.Expressions;

using CompanyHQ.Services.Appointments.Contracts.Models;
using CompanyHQ.Services.Postgres.Contracts.Enums;

namespace CompanyHQ.Services.Appointments.Contracts.Dtos;

/// <summary>The only place an <see cref="Appointment" /> becomes a wire DTO.</summary>
public static class AppointmentProjections
{
    /// <summary>The staff shape's owner fields, which the model reaches through its account.</summary>
    public static IReadOnlyDictionary<string, string> SearchAliases { get; } = new Dictionary<string, string>
    {
        [nameof(AdminAppointmentDto.UserFirstName)] = "User.FirstName",
        [nameof(AdminAppointmentDto.UserLastName)] = "User.LastName",
        [nameof(AdminAppointmentDto.UserEmail)] = "User.Email"
    };

    private static readonly Expression<Func<Appointment, ClientAppointmentDto>> ToClient =
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

    private static readonly Expression<Func<Appointment, ClientAppointmentDto>> ToAdminAsClient =
        appointment => (ClientAppointmentDto)new AdminAppointmentDto(
            appointment.Id,
            appointment.Title,
            appointment.ScheduledAt,
            appointment.Location,
            appointment.Notes,
            appointment.SourceKind,
            appointment.SourceId,
            appointment.CreatedAt,
            appointment.UpdatedAt,
            appointment.SourceKind == null,
            appointment.UserId,
            appointment.User!.FirstName,
            appointment.User.LastName,
            appointment.User.Email);

    public static Expression<Func<Appointment, ClientAppointmentDto>> For(ResponseAudience audience) =>
        audience is ResponseAudience.Admin ? ToAdminAsClient : ToClient;

    /// <summary>The DTO type <see cref="For" /> builds, which is what a search may name fields of.</summary>
    public static Type Shape(ResponseAudience audience) =>
        audience is ResponseAudience.Admin ? typeof(AdminAppointmentDto) : typeof(ClientAppointmentDto);
}
