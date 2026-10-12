using System.Text.Json.Serialization;

namespace CompanyHQ.Services.Rss.Contracts.Enums;

/// <summary>The syndication family a document or an advertised link belongs to.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FeedFormat>))]
public enum FeedFormat
{
    /// <summary>RSS 0.9x, 2.0, and RSS 1.0 (RDF).</summary>
    Rss = 0,

    /// <summary>Atom 1.0 and its 0.3 predecessor.</summary>
    Atom = 1
}
