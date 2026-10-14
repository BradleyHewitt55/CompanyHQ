using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Appointments.Contracts.Forms;

/// <summary>
/// Shared date rules. A past appointment is legitimate — members log visits after the fact — so the
/// only thing rejected is a value that is not a real instant.
/// </summary>
public static class AppointmentValidation
{
    public static IEnumerable<ValidationResult> ValidateScheduledAt(DateTimeOffset scheduledAt) =>
        scheduledAt == default
            ? [new ValidationResult("ScheduledAt is required.", [nameof(CreateAppointmentRequest.ScheduledAt)])]
            : [];

    /// <summary>
    /// Validates a request that never passed through MVC model binding. Requests from
    /// <c>IAppointmentScheduler</c> come from another service, not a request body, so nothing has
    /// checked them yet.
    /// </summary>
    public static void ThrowIfInvalid(object request)
    {
        var errors = new List<ValidationResult>();

        if (!Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            errors,
            validateAllProperties: true))
        {
            throw new ArgumentException(errors[0].ErrorMessage ?? "The request is invalid.");
        }
    }
}
