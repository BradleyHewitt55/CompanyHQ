namespace CompanyHQ.Services.Appointments.Contracts.Dtos;

/// <summary>
/// One appointment returned to the member whose diary it is. There is no admin variant: the DTO
/// describes the caller's own diary to them, so there is no privileged view of it to add.
/// </summary>
public sealed record ClientAppointmentDto(
    Guid Id,
    string Title,
    DateTimeOffset ScheduledAt,
    string? Location,
    string? Notes,
    /// <summary>Null when the member booked it themselves; otherwise the service that scheduled it.</summary>
    string? SourceKind,
    Guid? SourceId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    /// <summary>False for a source-linked row: the scheduling service owns it, so it is read-only here.</summary>
    bool CanManage);
