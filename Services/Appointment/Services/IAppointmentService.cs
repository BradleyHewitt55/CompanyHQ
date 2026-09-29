using CompanyHQ.Services.Appointments.Contracts.Dtos;
using CompanyHQ.Services.Appointments.Contracts.Forms;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Forms;

namespace CompanyHQ.Services.Appointments.Services;

/// <summary>
/// A member's own appointment diary. Every read and write is scoped to the acting user; a
/// source-linked row is read-only here because the scheduling service owns it.
/// </summary>
public interface IAppointmentService
{
    /// <summary>
    /// One page of the caller's appointments. <paramref name="upcoming" /> true returns everything
    /// from now forward, soonest first; false returns the past, most recent first.
    /// </summary>
    Task<PagedResult<ClientAppointmentDto>> ListAsync(
        Guid actingUserId,
        PageRequest page,
        bool upcoming = true,
        CancellationToken cancellationToken = default);

    Task<ClientAppointmentDto?> GetAsync(
        Guid appointmentId,
        Guid actingUserId,
        CancellationToken cancellationToken = default);

    Task<ClientAppointmentDto> CreateAsync(
        Guid actingUserId,
        CreateAppointmentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Returns null when the appointment is not the caller's, or is source-linked.</summary>
    Task<ClientAppointmentDto?> UpdateAsync(
        Guid appointmentId,
        Guid actingUserId,
        UpdateAppointmentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Returns false when the appointment is not the caller's, or is source-linked.</summary>
    Task<bool> DeleteAsync(
        Guid appointmentId,
        Guid actingUserId,
        CancellationToken cancellationToken = default);
}
