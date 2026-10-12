using System.Text.Json;
using System.Text.Json.Nodes;

namespace UniversalApi;

/// <summary>Thin native SDK: model naming, JSON value mapping, canonical CRUD/query; no driver or ORM.</summary>
public sealed class DbClient(RpcClient transport)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public Task<JsonNode> GetAsync<T>(Guid id, CancellationToken ct = default) where T : BaseModel =>
        CallAsync<T>("GET", new JsonObject { ["id"] = id.ToString() }, ct);
    public Task<JsonNode> SaveAsync<T>(JsonNode data, CancellationToken ct = default) where T : BaseModel =>
        CallAsync<T>("SAVE", new JsonObject { ["data"] = data.DeepClone() }, ct);
    public Task<JsonNode> UpdateAsync<T>(T model, CancellationToken ct = default) where T : BaseModel =>
        CallAsync<T>("UPDATE", new JsonObject { ["data"] = JsonSerializer.SerializeToNode(model, Json) }, ct);
    public Task<JsonNode> DeleteAsync<T>(Guid id, CancellationToken ct = default) where T : BaseModel =>
        CallAsync<T>("DELETE", new JsonObject { ["id"] = id.ToString() }, ct);
    public Task<JsonNode> FindAsync<T>(JsonNode query, CancellationToken ct = default) where T : BaseModel =>
        CallAsync<T>("FIND", new JsonObject { ["query"] = query.DeepClone() }, ct);
    public Task<JsonNode> ExistsAsync<T>(JsonNode query, CancellationToken ct = default) where T : BaseModel =>
        CallAsync<T>("EXISTS", new JsonObject { ["query"] = query.DeepClone() }, ct);
    public Task<JsonNode> CountAsync<T>(JsonNode query, CancellationToken ct = default) where T : BaseModel =>
        CallAsync<T>("COUNT", new JsonObject { ["query"] = query.DeepClone() }, ct);
    private Task<JsonNode> CallAsync<T>(string operation, JsonObject payload, CancellationToken ct)
    {
        payload["op"] = operation;
        payload["model"] = typeof(T).Name;
        return transport.CallAsync(payload, ct);
    }
}
