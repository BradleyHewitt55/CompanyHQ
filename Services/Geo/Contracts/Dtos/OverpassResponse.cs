using System.Text.Json.Serialization;

namespace CompanyHQ.Services.Geo.Contracts.Dtos;

/// <summary>The subset of an Overpass JSON response used to map nearby amenities.</summary>
internal sealed record OverpassResponse
{
    [JsonPropertyName("elements")]
    public IReadOnlyList<OverpassElement> Elements { get; init; } = [];
}
