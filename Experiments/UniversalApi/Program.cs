using System.Diagnostics;
using System.Text.Json.Nodes;
using Npgsql;
using UniversalApi;

var mode = args.ElementAtOrDefault(0) ?? "api";
var dbPort = int.Parse(Environment.GetEnvironmentVariable("UAL_DB_PORT") ?? "47121");
if (mode == "db")
{
    await using var pool = NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("UAL_POSTGRES")
        ?? throw new InvalidOperationException("UAL_POSTGRES must point to a disposable experimental database."));
    await new DatabaseService(pool).RunAsync(dbPort, CancellationToken.None);
    return;
}
var language = args.ElementAtOrDefault(1) ?? "csharp";
await using var db = new RpcClient();
await db.ConnectAsync(dbPort);
if (mode == "verify-db")
{
    await NativeSdkVerification.RunAsync(db);
    return;
}
JsonNode contract;
Func<string, JsonNode, CancellationToken, Task<JsonNode>> call;
Process? worker = null;
await using var service = new RpcClient();
if (language == "csharp")
{
    var native = new NativeRegistration(new UserController(new DbClient(db)));
    native.CacheCalls();
    contract = native.Contract;
    call = native.CallAsync;
}
else if (language == "python")
{
    var port = int.Parse(Environment.GetEnvironmentVariable("UAL_SERVICE_PORT") ?? "47122");
    var info = new ProcessStartInfo(Environment.GetEnvironmentVariable("UAL_PYTHON") ?? "python") { UseShellExecute = false, RedirectStandardOutput = true };
    info.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "python", "service.py"));
    info.ArgumentList.Add(port.ToString());
    info.ArgumentList.Add(dbPort.ToString());
    worker = Process.Start(info)!;
    if (await worker.StandardOutput.ReadLineAsync() != "ready") throw new InvalidOperationException("Worker failed startup.");
    await service.ConnectAsync(port);
    var registration = await service.CallAsync(new JsonObject { ["op"] = "SERVICE_REGISTER" });
    contract = registration["result"]!;
    call = (operation, payload, ct) => service.CallAsync(new JsonObject { ["op"] = "CALL", ["operation"] = operation, ["args"] = payload }, ct);
}
else throw new ArgumentException("Supported languages: csharp, python.");
foreach (var model in contract["models"]!.AsObject())
{
    var result = await db.CallAsync(new JsonObject { ["op"] = "MODEL_REGISTER", ["model"] = model.Key, ["fields"] = model.Value!.DeepClone() });
    if (result["error"] is not null) throw new InvalidOperationException("Incompatible model registration.");
}
// Metadata is extracted once. The HTTP roof knows neither User nor its DTO/model properties.
var builder = WebApplication.CreateBuilder();
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = Frames.MaximumBytes);
var app = builder.Build();
var exported = contract.DeepClone();
exported.AsObject().Remove("models"); // Private persistence metadata is never a public API export.
app.MapGet("/schema", () => Results.Json(exported));
foreach (var operation in contract["operations"]!.AsArray())
{
    if ((string?)operation!["policy"] != "anonymous-demo") throw new InvalidOperationException("This experiment does not implement authenticated policies.");
    var name = operation["name"]!.GetValue<string>();
    var route = contract["prefix"]!.GetValue<string>() + operation["route"]!.GetValue<string>();
    app.MapMethods(route, [operation["method"]!.GetValue<string>()], async (HttpContext context) =>
    {
        JsonNode envelope;
        try
        {
            var input = new JsonObject();
            foreach (var parameter in context.Request.RouteValues) input[parameter.Key] = parameter.Value?.ToString();
            if (context.Request.ContentLength is > 0 || context.Request.Headers.TransferEncoding.Count > 0)
                input["body"] = await JsonNode.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
            envelope = await call(name, input, context.RequestAborted);
        }
        catch (System.Text.Json.JsonException) { envelope = Frames.Error(400, "invalid_json"); }
        catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested) { envelope = Frames.Error(503, "service_timeout"); }
        catch (Exception exception) when (exception is IOException or System.Net.Sockets.SocketException) { envelope = Frames.Error(503, "service_unavailable"); }
        context.Response.StatusCode = (int?)envelope["status"] ?? 200;
        await context.Response.WriteAsJsonAsync(envelope["error"] is null ? envelope["result"] : envelope, context.RequestAborted);
    });
}
try { await app.RunAsync(); }
finally { if (worker is { HasExited: false }) { worker.Kill(entireProcessTree: true); worker.Dispose(); } }
