using System.Text.Json.Serialization;

namespace CompanyHQ.OpenStreetMap.Contracts.Dtos;

/// <summary>An OpenStreetMap node, way, or relation returned by Overpass.</summary>
internal sealed record OverpassElement
{
    [JsonPropertyName("lat")]
    public double? Latitude { get; init; }

    [JsonPropertyName("lon")]
    public double? Longitude { get; init; }

    [JsonPropertyName("center")]
    public OverpassCenter? Center { get; init; }

    [JsonPropertyName("tags")]
    public IReadOnlyDictionary<string, string>? Tags { get; init; }
}
