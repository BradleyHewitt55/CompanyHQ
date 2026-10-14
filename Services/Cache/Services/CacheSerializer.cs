using System.Text.Json;
using System.Text.Json.Serialization;

namespace CompanyHQ.Cache.Services;

/// <summary>
/// Turns cached values into JSON. Registered as a singleton so every writer and reader in the
/// process agrees on the payload shape — a mismatch here surfaces as silent misses, not errors.
/// </summary>
public sealed class CacheSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public T? Deserialize<T>(string payload) => JsonSerializer.Deserialize<T>(payload, Options);
}
