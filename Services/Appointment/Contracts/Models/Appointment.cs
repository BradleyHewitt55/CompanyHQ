using CompanyHQ.Services.Postgres.Contracts.Models;
using CompanyHQ.Services.Users.Contracts.Models;

namespace CompanyHQ.Services.Appointments.Contracts.Models;

/// <summary>One entry in a member's appointment diary.</summary>
public sealed class Appointment : BaseModel
{
    /// <summary>The member whose diary this is.</summary>
    public Guid UserId { get; set; }

    public User? User { get; set; }

    public required string Title { get; set; }

    public DateTimeOffset ScheduledAt { get; set; }

    public string? Location { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Which service scheduled this, or null when the member booked it themselves. Together with
    /// <see cref="SourceId" /> it is unique, which is what makes scheduling idempotent.
    /// </summary>
    public string? SourceKind { get; set; }

    /// <summary>The row in the calling service that drives this appointment.</summary>
    public Guid? SourceId { get; set; }
}
