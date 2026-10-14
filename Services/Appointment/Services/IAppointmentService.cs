using CompanyHQ.Appointments.Contracts.Dtos;
using CompanyHQ.Appointments.Contracts.Forms;
using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts.Forms;

namespace CompanyHQ.Appointments.Services;

/// <summary>
/// Appointment diaries. A member reaches only their own rows; a caller whose role grants
/// <c>appointments.manage</c> reaches every diary and gets the staff shape. A source-linked row is
/// read-only to both, because the scheduling service owns it.
/// </summary>
public interface IAppointmentService
{
    /// <summary>
    /// One page of appointments. <paramref name="upcoming" /> true returns everything from now
    /// forward, soonest first; false returns the past, most recent first.
    /// <paramref name="userId" /> narrows a manager's list to one diary and is ignored for a member.
    /// </summary>
    Task<PagedResult<ClientAppointmentDto>> ListAsync(
        Guid actingUserId,
        PageRequest page,
        bool upcoming = true,
        SearchRequest? search = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    Task<ClientAppointmentDto?> GetAsync(
        Guid appointmentId,
        Guid actingUserId,
        CancellationToken cancellationToken = default);

    /// <exception cref="UnauthorizedAccessException">The request names another diary and the caller is not a manager.</exception>
    /// <exception cref="ArgumentException">The named account does not exist.</exception>
    Task<ClientAppointmentDto> CreateAsync(
        Guid actingUserId,
        CreateAppointmentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Returns null when the appointment is out of the caller's reach, or is source-linked.</summary>
    Task<ClientAppointmentDto?> UpdateAsync(
        Guid appointmentId,
        Guid actingUserId,
        UpdateAppointmentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Returns false when the appointment is out of the caller's reach, or is source-linked.</summary>
    Task<bool> DeleteAsync(
        Guid appointmentId,
        Guid actingUserId,
        CancellationToken cancellationToken = default);
}
