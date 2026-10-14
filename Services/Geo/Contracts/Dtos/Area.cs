using CompanyHQ.Geo.Contracts.Enums;

namespace CompanyHQ.Geo.Contracts.Dtos;

/// <summary>
/// A named place. The coordinates are the rounded ones the lookup actually used, echoed back so a
/// client never has to keep the position it sent — and so nothing precise exists to hand on.
/// </summary>
public sealed record AreaDto(
    double Latitude,
    double Longitude,
    string DisplayName,
    string? Locality,
    string? Region,
    string? CountryCode,
    AreaPrecision Precision);
