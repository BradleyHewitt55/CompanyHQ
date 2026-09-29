using Microsoft.EntityFrameworkCore;

using CompanyHQ.Services.Appointments.Contracts.Dtos;
using CompanyHQ.Services.Appointments.Contracts.Forms;
using CompanyHQ.Services.Appointments.Contracts.Models;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Postgres.Extensions;
using CompanyHQ.Services.Postgres.Services;

namespace CompanyHQ.Services.Appointments.Services;

/// <inheritdoc cref="IAppointmentService" />
public sealed class AppointmentService(
    IPostgresService<Appointment> appointments,
    TimeProvider clock) : IAppointmentService
{
    public Task<PagedResult<ClientAppointmentDto>> ListAsync(
        Guid actingUserId,
        PageRequest page,
        bool upcoming = true,
        CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var mine = appointments.Query().Where(a => a.UserId == actingUserId);
        var query = upcoming ? mine.Where(a => a.ScheduledAt >= now) : mine.Where(a => a.ScheduledAt < now);

        return query.ToPagedResultAsync(
            page,
            AppointmentProjections.ToDto,
            items => upcoming
                ? items.OrderBy(a => a.ScheduledAt).ThenBy(a => a.Id)
                : items.OrderByDescending(a => a.ScheduledAt).ThenBy(a => a.Id),
            cancellationToken);
    }

    public Task<ClientAppointmentDto?> GetAsync(
        Guid appointmentId,
        Guid actingUserId,
        CancellationToken cancellationToken = default) =>
        appointments.Query()
            .Where(a => a.Id == appointmentId && a.UserId == actingUserId)
            .Select(AppointmentProjections.ToDto)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<ClientAppointmentDto> CreateAsync(
        Guid actingUserId,
        CreateAppointmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var appointment = await appointments.AddAsync(
            new Appointment
            {
                UserId = actingUserId,
                Title = request.Title.Trim(),
                ScheduledAt = request.ScheduledAt,
                Location = Clean(request.Location),
                Notes = Clean(request.Notes)
            },
            cancellationToken);

        return (await GetAsync(appointment.Id, actingUserId, cancellationToken))!;
    }

    public async Task<ClientAppointmentDto?> UpdateAsync(
        Guid appointmentId,
        Guid actingUserId,
        UpdateAppointmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var appointment = await appointments.Query()
            .FirstOrDefaultAsync(
                a => a.Id == appointmentId && a.UserId == actingUserId && a.SourceKind == null,
                cancellationToken);
        if (appointment is null)
        {
            return null;
        }

        appointment.Title = request.Title.Trim();
        appointment.ScheduledAt = request.ScheduledAt;
        appointment.Location = Clean(request.Location);
        appointment.Notes = Clean(request.Notes);
        await appointments.UpdateAsync(appointment, cancellationToken);

        return await GetAsync(appointmentId, actingUserId, cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        Guid appointmentId,
        Guid actingUserId,
        CancellationToken cancellationToken = default) =>
        await appointments.DeleteAsync(
            a => a.Id == appointmentId && a.UserId == actingUserId && a.SourceKind == null,
            cancellationToken) > 0;

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
