namespace CompanyHQ.Services.Geo.Contracts.Dtos;

/// <summary>One place of a configured kind inside the requested viewport.</summary>
/// <remarks>
/// <see cref="Address"/> and <see cref="Phone"/> are optional because OpenStreetMap tagging is
/// uneven: most places carry a name and a position, far fewer carry contact details. The client
/// hides the action rather than rendering a button that cannot do anything.
/// </remarks>
public sealed record NearbyPlaceDto(
    double Latitude,
    double Longitude,
    string Name,
    /// <summary>The OpenStreetMap <c>amenity</c> value, one of <c>Geo:NearbyPlaceKinds</c>.</summary>
    string Kind,
    string? Address = null,
    string? Phone = null);
