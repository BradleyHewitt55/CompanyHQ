using System.Text.Json.Serialization;

namespace CompanyHQ.Appointments.Contracts.Dtos;

/// <summary>One appointment returned to the member whose diary it is.</summary>
[JsonDerivedType(typeof(AdminAppointmentDto))]
public record ClientAppointmentDto(
    Guid Id,
    string Title,
    DateTimeOffset ScheduledAt,
    string? Location,
    string? Notes,
    /// <summary>Null when it was booked by hand; otherwise the service that scheduled it.</summary>
    string? SourceKind,
    Guid? SourceId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    /// <summary>False for a source-linked row: the scheduling service owns it, so it is read-only here.</summary>
    bool CanManage);

/// <summary>The staff view, which adds whose diary the appointment is in.</summary>
public sealed record AdminAppointmentDto(
    Guid Id,
    string Title,
    DateTimeOffset ScheduledAt,
    string? Location,
    string? Notes,
    string? SourceKind,
    Guid? SourceId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool CanManage,
    Guid UserId,
    string? UserFirstName,
    string? UserLastName,
    string UserEmail)
    : ClientAppointmentDto(Id, Title, ScheduledAt, Location, Notes, SourceKind, SourceId, CreatedAt, UpdatedAt, CanManage);
