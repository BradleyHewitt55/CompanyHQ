using CompanyHQ.ServiceCatalog.Contracts;

using CompanyHQ.Appointments.Contracts.Dtos;
using CompanyHQ.Appointments.Contracts.Forms;
using CompanyHQ.Appointments.Contracts.Models;
using CompanyHQ.Postgres.Contracts;
using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts.Enums;
using CompanyHQ.Postgres.Contracts.Forms;
using CompanyHQ.Persistence.Services;
using CompanyHQ.Users.Contracts.Models;
using CompanyHQ.Users.Services;

namespace CompanyHQ.Appointments.Services;

/// <inheritdoc cref="IAppointmentService" />
public sealed class AppointmentService(IServiceAvailability? availability,
    IRepository<Appointment> appointments,
    IRepository<User> users,
    IUserAuthorizationService authorization,
    TimeProvider clock) : IAppointmentService
{
    /// <summary>Retains the original strict constructor for existing consumers.</summary>
    public AppointmentService(
    IRepository<Appointment> appointments,
    IRepository<User> users,
    IUserAuthorizationService authorization,
    TimeProvider clock) : this(null, appointments, users, authorization, clock) { }

    public async Task<PagedResult<ClientAppointmentDto>> ListAsync(
        Guid actingUserId,
        PageRequest page,
        bool upcoming = true,
        SearchRequest? search = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        await (availability?.RequireAsync("appointments", false, cancellationToken) ?? Task.CompletedTask);
        var manager = await IsManagerAsync(actingUserId, cancellationToken);
        var audience = Audience(manager);
        var now = clock.GetUtcNow();

        var query = Reachable(actingUserId, manager);
        if (manager && userId is { } owner)
        {
            query = query.Where(a => a.UserId == owner);
        }

        query = (upcoming ? query.Where(a => a.ScheduledAt >= now) : query.Where(a => a.ScheduledAt < now))
            .Search(search?.Field, search?.Keyword, AppointmentProjections.Shape(audience), AppointmentProjections.SearchAliases);

        var ordered = upcoming
            ? query.OrderBy(a => a.ScheduledAt).ThenBy(a => a.Id)
            : query.OrderByDescending(a => a.ScheduledAt).ThenBy(a => a.Id);
        var result = await ordered.ToPageAsync(page.Skip, page.PageSize, AppointmentProjections.For(audience), cancellationToken);

        return new PagedResult<ClientAppointmentDto>(result.Items, page.Page, page.PageSize, result.Total);
    }

    public async Task<ClientAppointmentDto?> GetAsync(
        Guid appointmentId,
        Guid actingUserId,
        CancellationToken cancellationToken = default)
    {
        await (availability?.RequireAsync("appointments", false, cancellationToken) ?? Task.CompletedTask);
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
        await (availability?.RequireAsync("appointments", false, cancellationToken) ?? Task.CompletedTask);
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
        await (availability?.RequireAsync("appointments", false, cancellationToken) ?? Task.CompletedTask);
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
        await (availability?.RequireAsync("appointments", false, cancellationToken) ?? Task.CompletedTask);
        var manager = await IsManagerAsync(actingUserId, cancellationToken);
        return await appointments.DeleteAsync(
            a => a.Id == appointmentId && a.SourceKind == null && (manager || a.UserId == actingUserId),
            cancellationToken) > 0;
    }

    /// <summary>The owner filter is the authorization: only a manager's query goes without it.</summary>
    private IQuery<Appointment> Reachable(Guid actingUserId, bool manager) =>
        manager ? appointments.Query() : appointments.Query().Where(a => a.UserId == actingUserId);

    private Task<bool> IsManagerAsync(Guid userId, CancellationToken cancellationToken) =>
        authorization.HasPermissionAsync(userId, AccessPermissions.ManageAppointments, cancellationToken);

    private static ResponseAudience Audience(bool manager) =>
        manager ? ResponseAudience.Admin : ResponseAudience.Owner;

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
