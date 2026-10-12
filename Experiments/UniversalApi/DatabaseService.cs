using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using Npgsql;
using NpgsqlTypes;

namespace UniversalApi;

public sealed class DatabaseService(NpgsqlDataSource pool)
{
    private readonly ConcurrentDictionary<string, JsonNode> models = new(StringComparer.Ordinal);
    public async Task RunAsync(int port, CancellationToken ct)
    {
        await using (var command = pool.CreateCommand("CREATE TABLE IF NOT EXISTS ual_record (model text NOT NULL, id uuid NOT NULL, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, data jsonb NOT NULL, PRIMARY KEY(model,id))"))
            await command.ExecuteNonQueryAsync(ct);
        var server = new TcpListener(IPAddress.Loopback, port);
        server.Start();
        Console.WriteLine("DB ready");
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await server.AcceptTcpClientAsync(ct);
                _ = ServeAsync(client, ct);
            }
        }
        finally { server.Stop(); }
    }
    private async Task ServeAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            client.NoDelay = true;
            var stream = client.GetStream();
            try
            {
                while (await Frames.ReadAsync(stream, ct) is { } request)
                {
                    JsonNode response;
                    try { response = await ExecuteAsync(request, ct); }
                    catch (ArgumentException) { response = Frames.Error(400, "invalid_request"); }
                    catch (FormatException) { response = Frames.Error(400, "invalid_request"); }
                    catch (InvalidOperationException) { response = Frames.Error(400, "invalid_request"); }
                    catch (PostgresException exception) { response = Frames.Error(exception.SqlState == "23505" ? 409 : 503, "store_rejected"); }
                    catch (NpgsqlException) { response = Frames.Error(503, "store_unavailable"); }
                    await Frames.WriteAsync(stream, response, ct);
                }
            }
            catch (Exception exception) when (exception is IOException or System.Text.Json.JsonException or OperationCanceledException) { }
        }
    }
    private async Task<JsonNode> ExecuteAsync(JsonNode request, CancellationToken ct)
    {
        var op = (string?)request["op"] ?? throw new ArgumentException();
        var model = (string?)request["model"] ?? throw new ArgumentException();
        if (op == "MODEL_REGISTER")
        {
            var fields = request["fields"] ?? throw new ArgumentException();
            if (!model.All(char.IsAsciiLetterOrDigit) || fields["id"]?.GetValue<string>() != "uuid"
                || fields["createdAt"]?.GetValue<string>() != "datetime" || fields["updatedAt"]?.GetValue<string>() != "datetime")
                return Frames.Error(400, "invalid_model");
            var registered = models.GetOrAdd(model, fields.DeepClone());
            return JsonNode.DeepEquals(registered, fields) ? Frames.Result(JsonValue.Create(true)) : Frames.Error(409, "model_conflict");
        }
        if (!models.TryGetValue(model, out var schema)) return Frames.Error(400, "unregistered_model");
        await using var command = pool.CreateCommand();
        command.Parameters.AddWithValue("model", model);
        var select = "SELECT data || jsonb_build_object('id',id,'createdAt',created_at,'updatedAt',updated_at) FROM ual_record WHERE model=@model";
        if (op is "GET" or "DELETE" or "SAVE" or "UPDATE")
        {
            if (op == "UPDATE" && request["data"]?["id"] is null) return Frames.Error(400, "missing_id");
            var id = request["id"] is { } key ? Guid.Parse(key.GetValue<string>())
                : request["data"]?["id"] is { } dataId ? Guid.Parse(dataId.GetValue<string>()) : Guid.CreateVersion7();
            if (op == "SAVE" && id == Guid.Empty) id = Guid.CreateVersion7();
            command.Parameters.AddWithValue("id", id);
            if (op == "GET") command.CommandText = select + " AND id=@id";
            if (op == "DELETE")
            {
                command.CommandText = "DELETE FROM ual_record WHERE model=@model AND id=@id";
                return Frames.Result(JsonValue.Create(await command.ExecuteNonQueryAsync(ct) > 0));
            }
            if (op is "SAVE" or "UPDATE")
            {
                var data = (request["data"] ?? throw new ArgumentException()).DeepClone().AsObject();
                foreach (var field in data)
                    if (schema[field.Key] is null) return Frames.Error(400, "unknown_field");
                data.Remove("id"); data.Remove("createdAt"); data.Remove("updatedAt");
                command.Parameters.AddWithValue("data", NpgsqlDbType.Jsonb, data.ToJsonString());
                command.CommandText = op == "SAVE"
                    ? "INSERT INTO ual_record VALUES (@model,@id,clock_timestamp(),clock_timestamp(),@data) RETURNING data || jsonb_build_object('id',id,'createdAt',created_at,'updatedAt',updated_at)"
                    : "UPDATE ual_record SET data=@data,updated_at=clock_timestamp() WHERE model=@model AND id=@id RETURNING data || jsonb_build_object('id',id,'createdAt',created_at,'updatedAt',updated_at)";
            }
            var value = await command.ExecuteScalarAsync(ct);
            return value is string json ? Frames.Result(JsonNode.Parse(json)) : Frames.Error(404, "not_found");
        }
        if (op is not ("FIND" or "EXISTS" or "COUNT")) return Frames.Error(400, "unknown_operation");
        var where = request["query"]?["where"] is { } predicate ? " AND " + Predicate(predicate, schema, command) : "";
        if (op is "EXISTS" or "COUNT")
        {
            command.CommandText = op == "EXISTS" ? "SELECT EXISTS(SELECT 1 FROM ual_record WHERE model=@model" + where + ")"
                : "SELECT count(*) FROM ual_record WHERE model=@model" + where;
            return Frames.Result(JsonValue.Create(await command.ExecuteScalarAsync(ct)));
        }
        var fieldName = (string?)request["query"]?["order"] ?? "createdAt";
        var fieldSql = Field(fieldName, schema);
        command.CommandText = select + where + " ORDER BY " + fieldSql + ((bool?)request["query"]?["descending"] == true ? " DESC" : " ASC") + ",id LIMIT @limit OFFSET @offset";
        command.Parameters.AddWithValue("limit", Math.Clamp((int?)request["query"]?["limit"] ?? 20, 1, 100));
        command.Parameters.AddWithValue("offset", Math.Clamp((int?)request["query"]?["offset"] ?? 0, 0, 10000));
        var rows = new JsonArray();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) rows.Add(JsonNode.Parse(reader.GetString(0)));
        return Frames.Result(rows);
    }
    private static string Field(string name, JsonNode schema)
    {
        if (!name.All(char.IsAsciiLetterOrDigit) || schema[name] is null) throw new ArgumentException();
        return name switch { "id" => "id::text", "createdAt" => "created_at::text", "updatedAt" => "updated_at::text", _ => $"data->>'{name}'" };
    }
    private static string Predicate(JsonNode node, JsonNode schema, NpgsqlCommand command, int depth = 0)
    {
        if (depth > 8 || command.Parameters.Count > 100) throw new ArgumentException();
        var op = (string?)node["op"];
        if (op is "and" or "or")
        {
            var children = node["args"]!.AsArray();
            if (children.Count is < 1 or > 10) throw new ArgumentException();
            return "(" + string.Join(op == "and" ? " AND " : " OR ", children.Select(child => Predicate(child!, schema, command, depth + 1))) + ")";
        }
        var field = Field(node["field"]!.GetValue<string>(), schema);
        if (op == "null") return field + " IS NULL";
        var key = "p" + command.Parameters.Count;
        if (op == "in")
        {
            command.Parameters.AddWithValue(key, node["value"]!.AsArray().Select(value => value!.ToString()).ToArray());
            return field + " = ANY(@" + key + ")";
        }
        command.Parameters.AddWithValue(key, node["value"]?.ToString() ?? throw new ArgumentException());
        if (op == "contains") return "strpos(lower(" + field + "),lower(@" + key + ")) > 0";
        // The first vertical slice exports string fields. Numeric/date comparisons use an explicit
        // cast selected by cached model metadata, never a caller-supplied SQL type.
        var type = schema[node["field"]!.GetValue<string>()]!.GetValue<string>();
        var cast = type is "int32" or "int64" or "decimal" ? "numeric" : type is "datetime" ? "timestamptz" : type is "date" ? "date" : "text";
        var symbol = op switch { "eq" => "=", "ne" => "<>", "gt" => ">", "lt" => "<", _ => throw new ArgumentException() };
        return $"({field})::{cast} {symbol} @{key}::{cast}";
    }
}
