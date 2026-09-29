using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Services.Appointments.Contracts.Forms;

/// <summary>
/// What one service asks for when it schedules on a member's behalf. Never bound from a request
/// body: <see cref="SourceKind" /> and <see cref="SourceId" /> are the calling service's identity,
/// and a member who could set them would be able to overwrite another service's rows.
/// </summary>
/// <param name="SourceKind">
/// Which service owns this row, as free text rather than an enum so a new service can link itself
/// without editing Appointment. Use the name of the entity that drives it.
/// </param>
/// <param name="SourceId">The id of the row in the calling service that drives this appointment.</param>
public sealed record ScheduleAppointmentRequest(
    [Required, MaxLength(160), RegularExpression(@".*\S.*")] string Title,
    DateTimeOffset ScheduledAt,
    [Required, MaxLength(64)] string SourceKind,
    [Required] Guid SourceId,
    [MaxLength(200)] string? Location = null,
    [MaxLength(2_000)] string? Notes = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context) =>
        AppointmentValidation.ValidateScheduledAt(ScheduledAt);
}
