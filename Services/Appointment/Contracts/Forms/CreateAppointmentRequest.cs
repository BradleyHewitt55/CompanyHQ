using System.ComponentModel.DataAnnotations;

using CompanyHQ.Services.Appointments.Contracts.Enums;

namespace CompanyHQ.Services.Appointments.Contracts.Forms;

/// <summary>Books independently manageable appointments in an account diary.</summary>
public sealed record CreateAppointmentRequest(
    [Required, MaxLength(160), RegularExpression(@".*\S.*")] string Title,
    DateTimeOffset ScheduledAt,
    [MaxLength(200)] string? Location = null,
    [MaxLength(2_000)] string? Notes = null,
    Guid? UserId = null,
    [EnumDataType(typeof(AppointmentRepeat))] AppointmentRepeat Repeat = AppointmentRepeat.None,
    [Range(1, 100)] int Occurrences = 1) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(Title) || Title.Length > 160)
            yield return new ValidationResult("Title is required and must be at most 160 characters.", [nameof(Title)]);
        if (Location?.Length > 200 || Notes?.Length > 2_000)
            yield return new ValidationResult("Location or notes exceed their maximum length.", [nameof(Location), nameof(Notes)]);
        if (!Enum.IsDefined(Repeat))
            yield return new ValidationResult("Choose a supported repeat frequency.", [nameof(Repeat)]);
        if (Occurrences is < 1 or > 100)
            yield return new ValidationResult("Occurrences must be between 1 and 100.", [nameof(Occurrences)]);
        foreach (var error in AppointmentValidation.ValidateScheduledAt(ScheduledAt))
        {
            yield return error;
        }

        if (Repeat == AppointmentRepeat.None && Occurrences != 1)
        {
            yield return new ValidationResult("Choose a repeat frequency for multiple appointments.", [nameof(Repeat)]);
        }

        if (Occurrences is >= 1 and <= 100 && Enum.IsDefined(Repeat))
        {
            var validDate = true;
            try
            {
                ScheduledAtFor(Occurrences - 1);
            }
            catch (ArgumentOutOfRangeException)
            {
                validDate = false;
            }

            if (!validDate)
            {
                yield return new ValidationResult("The repeat dates exceed the supported date range.", [nameof(ScheduledAt)]);
            }
        }
    }

    // Anchor every month to the original day so January 31 becomes February 28 and March 31.
    public DateTimeOffset ScheduledAtFor(int occurrence) => Repeat switch
    {
        AppointmentRepeat.Daily => ScheduledAt.AddDays(occurrence),
        AppointmentRepeat.Weekly => ScheduledAt.AddDays(7 * occurrence),
        AppointmentRepeat.Monthly => ScheduledAt.AddMonths(occurrence),
        _ => ScheduledAt
    };
}
