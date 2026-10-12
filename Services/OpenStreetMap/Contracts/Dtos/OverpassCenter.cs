using System.Text.Json.Serialization;

namespace CompanyHQ.Services.OpenStreetMap.Contracts.Dtos;

/// <summary>The representative point Overpass returns for a way or relation.</summary>
internal sealed record OverpassCenter
{
    [JsonPropertyName("lat")]
    public double Latitude { get; init; }

    [JsonPropertyName("lon")]
    public double Longitude { get; init; }
}
