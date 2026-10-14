using CompanyHQ.Appointments.Contracts.Dtos;
using CompanyHQ.Appointments.Contracts.Forms;

namespace CompanyHQ.Appointments.Contracts.Ports;

/// <summary>
/// The port another service calls to put an appointment in a member's diary on their behalf.
/// </summary>
/// <remarks>
/// It lives in the dependency-free Contracts assembly so a caller references only that, never the
/// Appointment service itself; DI supplies the implementation when Appointment is plugged in. It is
/// deliberately narrower than <c>IAppointmentService</c>: a calling service can create, move, and
/// cancel the rows it owns, and can do nothing else to a member's diary.
/// </remarks>
public interface IAppointmentScheduler
{
    /// <summary>
    /// Creates or moves the single appointment identified by the request's source link. Calling it
    /// twice for the same source updates the existing row rather than adding a second one, so a
    /// caller may run it on every save without tracking what it created before.
    /// </summary>
    Task<ClientAppointmentDto> ScheduleAsync(
        Guid ownerUserId,
        ScheduleAppointmentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the appointment for one source link. Returns false when there was nothing to remove,
    /// which is the normal result for a source that never scheduled anything.
    /// </summary>
    Task<bool> CancelAsync(
        string sourceKind,
        Guid sourceId,
        CancellationToken cancellationToken = default);
}
