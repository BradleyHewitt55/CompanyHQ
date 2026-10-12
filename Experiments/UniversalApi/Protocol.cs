using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace UniversalApi;

// Bounded newline-delimited JSON v1. One outstanding operation per persistent connection.
public sealed class RpcClient : IAsyncDisposable
{
    private readonly TcpClient socket = new();
    private readonly SemaphoreSlim gate = new(1);
    private Stream? stream;
    public async Task ConnectAsync(int port)
    {
        await socket.ConnectAsync("127.0.0.1", port);
        socket.NoDelay = true;
        stream = socket.GetStream();
    }
    public async Task<JsonNode> CallAsync(JsonNode request, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(10));
            await Frames.WriteAsync(stream!, request, deadline.Token);
            return await Frames.ReadAsync(stream!, deadline.Token) ?? throw new IOException("Peer disconnected.");
        }
        catch
        {
            socket.Dispose(); // A cancelled response must never be mistaken for the next response.
            throw;
        }
        finally { gate.Release(); }
    }
    public ValueTask DisposeAsync() { socket.Dispose(); gate.Dispose(); return ValueTask.CompletedTask; }
}

public static class Frames
{
    public const int MaximumBytes = 65536;
    public static async Task<JsonNode?> ReadAsync(Stream stream, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var next = new byte[1];
        while (await stream.ReadAsync(next, ct) != 0)
        {
            if (next[0] == 10) return JsonNode.Parse(buffer.ToArray()) ?? throw new JsonException();
            if (buffer.Length >= MaximumBytes) throw new InvalidDataException("Frame too large.");
            buffer.WriteByte(next[0]);
        }
        if (buffer.Length != 0) throw new InvalidDataException("Incomplete frame.");
        return null;
    }
    public static async Task WriteAsync(Stream stream, JsonNode node, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(node.ToJsonString() + "\n");
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("Frame too large.");
        await stream.WriteAsync(bytes, ct);
    }
    public static JsonNode Result(JsonNode? value) => new JsonObject { ["result"] = value };
    public static JsonNode Error(int status, string code) => new JsonObject { ["status"] = status, ["error"] = code };
}
