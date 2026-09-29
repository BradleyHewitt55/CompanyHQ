namespace CompanyHQ.Services.Geo.Contracts.Dtos;

/// <summary>The answer to one viewport request: the pins, and which tiles are actually accounted for.</summary>
/// <remarks>
/// <see cref="Tiles"/> exists so a client can mark exactly the tiles it now holds. Without it a
/// partially served request would look complete and the tiles that failed would never be asked for
/// again. <see cref="Incomplete"/> is true when at least one requested tile was neither cached nor
/// fetchable — only a request that is incomplete <i>and</i> empty is worth a 503.
/// </remarks>
public sealed record NearbyPlacesResultDto(
    IReadOnlyList<NearbyPlaceDto> Places,
    IReadOnlyList<string> Tiles,
    bool Incomplete);
