using Microsoft.EntityFrameworkCore;

using CompanyHQ.Services.Appointments.Contracts.Dtos;
using CompanyHQ.Services.Appointments.Contracts.Forms;
using CompanyHQ.Services.Appointments.Contracts.Models;
using CompanyHQ.Services.Appointments.Contracts.Ports;
using CompanyHQ.Services.Postgres.Contracts.Enums;
using CompanyHQ.Services.Postgres.Services;

namespace CompanyHQ.Services.Appointments.Services;

/// <inheritdoc cref="IAppointmentScheduler" />
/// <remarks>
/// <see cref="IPostgresService{TModel}"/> shares the scoped context with its caller, so a caller
/// already inside a transaction gets the scheduled appointment committed or rolled back with the
/// change that caused it. Do not open another transaction here or that stops being true.
/// </remarks>
public sealed class AppointmentScheduler(IPostgresService<Appointment> appointments) : IAppointmentScheduler
{
    public async Task<ClientAppointmentDto> ScheduleAsync(
        Guid ownerUserId,
        ScheduleAppointmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        AppointmentValidation.ThrowIfInvalid(request);

        var appointment = await appointments.Query()
            .FirstOrDefaultAsync(
                candidate => candidate.SourceKind == request.SourceKind && candidate.SourceId == request.SourceId,
                cancellationToken);

        var isNew = appointment is null;
        appointment ??= new Appointment
        {
            UserId = ownerUserId,
            SourceKind = request.SourceKind,
            SourceId = request.SourceId,
            Title = string.Empty
        };

        appointment.Title = request.Title.Trim();
        appointment.ScheduledAt = request.ScheduledAt;
        appointment.Location = Clean(request.Location);
        appointment.Notes = Clean(request.Notes);

        appointment = isNew
            ? await appointments.AddAsync(appointment, cancellationToken)
            : await appointments.UpdateAsync(appointment, cancellationToken);

        return await appointments.Query()
            .Where(candidate => candidate.Id == appointment.Id)
            .Select(AppointmentProjections.For(ResponseAudience.Owner))
            .FirstAsync(cancellationToken);
    }

    public async Task<bool> CancelAsync(
        string sourceKind,
        Guid sourceId,
        CancellationToken cancellationToken = default) =>
        await appointments.DeleteAsync(
            candidate => candidate.SourceKind == sourceKind && candidate.SourceId == sourceId,
            cancellationToken) > 0;

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
