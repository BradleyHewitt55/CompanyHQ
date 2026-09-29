using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Services.Appointments.Contracts.Forms;

/// <summary>Adds one appointment to the caller's diary.</summary>
public sealed record CreateAppointmentRequest(
    [Required, MaxLength(160), RegularExpression(@".*\S.*")] string Title,
    DateTimeOffset ScheduledAt,
    [MaxLength(200)] string? Location = null,
    [MaxLength(2_000)] string? Notes = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context) =>
        AppointmentValidation.ValidateScheduledAt(ScheduledAt);
}
