using Microsoft.EntityFrameworkCore;

using CompanyHQ.Services.Appointments.Contracts.Dtos;
using CompanyHQ.Services.Appointments.Contracts.Forms;
using CompanyHQ.Services.Appointments.Contracts.Models;
using CompanyHQ.Services.Postgres.Contracts;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Enums;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Postgres.Extensions;
using CompanyHQ.Services.Postgres.Services;
using CompanyHQ.Services.Users.Contracts.Models;
using CompanyHQ.Services.Users.Services;

namespace CompanyHQ.Services.Appointments.Services;

/// <inheritdoc cref="IAppointmentService" />
public sealed class AppointmentService(
    IPostgresService<Appointment> appointments,
    IPostgresService<User> users,
    IUserAuthorizationService authorization,
    TimeProvider clock) : IAppointmentService
{
    public async Task<PagedResult<ClientAppointmentDto>> ListAsync(
        Guid actingUserId,
        PageRequest page,
        bool upcoming = true,
        SearchRequest? search = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var manager = await IsManagerAsync(actingUserId, cancellationToken);
        var audience = Audience(manager);
        var now = clock.GetUtcNow();

        var query = Reachable(actingUserId, manager);
        if (manager && userId is { } owner)
        {
            query = query.Where(a => a.UserId == owner);
        }

        query = (upcoming ? query.Where(a => a.ScheduledAt >= now) : query.Where(a => a.ScheduledAt < now))
            .Search(search, AppointmentProjections.Shape(audience), AppointmentProjections.SearchAliases);

        return await query.ToPagedResultAsync(
            page,
            AppointmentProjections.For(audience),
            items => upcoming
                ? items.OrderBy(a => a.ScheduledAt).ThenBy(a => a.Id)
                : items.OrderByDescending(a => a.ScheduledAt).ThenBy(a => a.Id),
            cancellationToken);
    }

    public async Task<ClientAppointmentDto?> GetAsync(
        Guid appointmentId,
        Guid actingUserId,
        CancellationToken cancellationToken = default)
    {
        var manager = await IsManagerAsync(actingUserId, cancellationToken);
        return await Reachable(actingUserId, manager)
            .Where(a => a.Id == appointmentId)
            .Select(AppointmentProjections.For(Audience(manager)))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ClientAppointmentDto> CreateAsync(
        Guid actingUserId,
        CreateAppointmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        AppointmentValidation.ThrowIfInvalid(request);

        var ownerUserId = request.UserId ?? actingUserId;
        if (ownerUserId != actingUserId)
        {
            if (!await IsManagerAsync(actingUserId, cancellationToken))
            {
                throw new UnauthorizedAccessException();
            }

            if (!await users.ExistsAsync(ownerUserId, cancellationToken))
            {
                throw new ArgumentException("That account does not exist.");
            }
        }

        var created = await appointments.AddRangeAsync(
            Enumerable.Range(0, request.Occurrences).Select(occurrence => new Appointment
            {
                UserId = ownerUserId,
                Title = request.Title.Trim(),
                ScheduledAt = request.ScheduledAtFor(occurrence).ToUniversalTime(),
                Location = Clean(request.Location),
                Notes = Clean(request.Notes)
            }),
            cancellationToken);

        return (await GetAsync(created[0].Id, actingUserId, cancellationToken))!;
    }

    public async Task<ClientAppointmentDto?> UpdateAsync(
        Guid appointmentId,
        Guid actingUserId,
        UpdateAppointmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var manager = await IsManagerAsync(actingUserId, cancellationToken);
        var appointment = await Reachable(actingUserId, manager)
            .FirstOrDefaultAsync(a => a.Id == appointmentId && a.SourceKind == null, cancellationToken);
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
        CancellationToken cancellationToken = default)
    {
        var manager = await IsManagerAsync(actingUserId, cancellationToken);
        return await appointments.DeleteAsync(
            a => a.Id == appointmentId && a.SourceKind == null && (manager || a.UserId == actingUserId),
            cancellationToken) > 0;
    }

    /// <summary>The owner filter is the authorization: only a manager's query goes without it.</summary>
    private IQueryable<Appointment> Reachable(Guid actingUserId, bool manager) =>
        manager ? appointments.Query() : appointments.Query().Where(a => a.UserId == actingUserId);

    private Task<bool> IsManagerAsync(Guid userId, CancellationToken cancellationToken) =>
        authorization.HasPermissionAsync(userId, AccessPermissions.ManageAppointments, cancellationToken);

    private static ResponseAudience Audience(bool manager) =>
        manager ? ResponseAudience.Admin : ResponseAudience.Owner;

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
