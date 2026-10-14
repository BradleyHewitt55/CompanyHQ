namespace CompanyHQ.Geo.Contracts.Dtos;

/// <summary>One cached map tile's worth of nearby places, with the time it was fetched.</summary>
/// <remarks>
/// The timestamp is what makes stale-while-revalidate possible: Redis only knows the hard TTL, so
/// the soft one has to travel inside the entry. An empty <see cref="Places"/> is a real answer —
/// "there is nothing here" — and is only ever written after a provider call that actually
/// succeeded. A failure is never written at all.
/// </remarks>
public sealed record NearbyTileDto(DateTimeOffset FetchedAt, IReadOnlyList<NearbyPlaceDto> Places);
