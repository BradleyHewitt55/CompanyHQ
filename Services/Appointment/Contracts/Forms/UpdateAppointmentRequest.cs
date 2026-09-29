using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Services.Appointments.Contracts.Forms;

/// <summary>Replaces one of the caller's own manually booked appointments wholesale.</summary>
public sealed record UpdateAppointmentRequest(
    [Required, MaxLength(160), RegularExpression(@".*\S.*")] string Title,
    DateTimeOffset ScheduledAt,
    [MaxLength(200)] string? Location = null,
    [MaxLength(2_000)] string? Notes = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context) =>
        AppointmentValidation.ValidateScheduledAt(ScheduledAt);
}
