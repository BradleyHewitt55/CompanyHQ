using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace UniversalApi;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ApiServiceAttribute(string name, string prefix, string requires) : Attribute
{
    public string Name { get; } = name;
    public string Prefix { get; } = prefix;
    public string Requires { get; } = requires;
}
[AttributeUsage(AttributeTargets.Method)]
public sealed class OperationAttribute(string method, string route, string input, string output) : Attribute
{
    public string Method { get; } = method;
    public string Route { get; } = route;
    public string Input { get; } = input;
    public string Output { get; } = output;
}
[AttributeUsage(AttributeTargets.Class)]
public sealed class ApiDtoAttribute : Attribute;
[AttributeUsage(AttributeTargets.Class)]
public sealed class DbModelAttribute : Attribute;

public abstract record BaseModel(Guid Id, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
[DbModel]
public sealed record User(Guid Id, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string Name, string Email)
    : BaseModel(Id, CreatedAt, UpdatedAt);
[ApiDto]
public sealed record UserDto(Guid Id, string Name, string Email, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
[ApiDto]
public sealed record CreateUserDto(string Name, string Email);

// Only this service owns user validation, mapping, DTOs and models. The roof sees metadata and calls.
[ApiService("users", "/users", "db:primary")]
public sealed class UserController(DbClient db)
{
    [Operation("POST", "", "CreateUserDto", "UserDto")]
    public async Task<JsonNode> Create(JsonNode args, CancellationToken ct)
    {
        if (args["body"] is not JsonObject body
            || body["name"] is not JsonValue name || !name.TryGetValue<string>(out var text) || string.IsNullOrWhiteSpace(text)
            || body["email"] is not JsonValue email || !email.TryGetValue<string>(out var address) || !address.Contains('@'))
            return Frames.Error(400, "invalid_user");
        return ToDto(await db.SaveAsync<User>(new JsonObject { ["name"] = text, ["email"] = address }, ct));
    }
    [Operation("GET", "/{id}", "uuid", "UserDto")]
    public async Task<JsonNode> Get(JsonNode args, CancellationToken ct) =>
        Guid.TryParse((string?)args["id"], out var id)
            ? ToDto(await db.GetAsync<User>(id, ct)) : Frames.Error(400, "invalid_id");

    private static JsonNode ToDto(JsonNode response)
    {
        if (response["error"] is not null) return response;
        var user = response["result"]!;
        // Explicit export projection: a new internal model field is never automatically public.
        return Frames.Result(new JsonObject
        {
            ["id"] = user["id"]!.DeepClone(),
            ["name"] = user["name"]!.DeepClone(),
            ["email"] = user["email"]!.DeepClone(),
            ["createdAt"] = user["createdAt"]!.DeepClone(),
            ["updatedAt"] = user["updatedAt"]!.DeepClone()
        });
    }
}

public sealed class NativeRegistration(object controller)
{
    private readonly Dictionary<string, Func<JsonNode, CancellationToken, Task<JsonNode>>> calls = [];
    public JsonNode Contract { get; } = Build(controller);
    public void CacheCalls()
    {
        foreach (var method in controller.GetType().GetMethods().Where(method => method.IsDefined(typeof(OperationAttribute))))
            calls.Add(method.Name, method.CreateDelegate<Func<JsonNode, CancellationToken, Task<JsonNode>>>(controller));
    }
    public Task<JsonNode> CallAsync(string operation, JsonNode args, CancellationToken ct) => calls[operation](args, ct);
    private static JsonNode Build(object controller)
    {
        var type = controller.GetType();
        var service = type.GetCustomAttribute<ApiServiceAttribute>()!;
        var assembly = type.Assembly;
        var dtos = new JsonObject();
        var models = new JsonObject();
        foreach (var shape in assembly.GetTypes())
        {
            var target = shape.IsDefined(typeof(ApiDtoAttribute)) ? dtos : shape.IsDefined(typeof(DbModelAttribute)) ? models : null;
            if (target is null) continue;
            var fields = new JsonObject();
            foreach (var property in shape.GetProperties())
                fields[JsonNamingPolicy.CamelCase.ConvertName(property.Name)] = Primitive(property.PropertyType);
            target[shape.Name] = fields;
        }
        return new JsonObject
        {
            ["version"] = 1,
            ["service"] = service.Name,
            ["prefix"] = service.Prefix,
            ["requires"] = service.Requires,
            ["dtos"] = dtos,
            ["models"] = models,
            ["operations"] = new JsonArray(type.GetMethods().Where(method => method.IsDefined(typeof(OperationAttribute))).Select(method =>
            {
                var marker = method.GetCustomAttribute<OperationAttribute>()!;
                return (JsonNode)new JsonObject
                {
                    ["name"] = method.Name,
                    ["method"] = marker.Method,
                    ["route"] = marker.Route,
                    ["input"] = marker.Input,
                    ["output"] = marker.Output,
                    ["errors"] = new JsonArray(400, 404, 409, 503),
                    ["policy"] = "anonymous-demo"
                };
            }).ToArray())
        };
    }
    private static string Primitive(Type type) => type == typeof(string) ? "string" : type == typeof(Guid) ? "uuid"
        : type == typeof(DateTimeOffset) ? "datetime" : type == typeof(bool) ? "bool" : type == typeof(int) ? "int32"
        : type == typeof(long) ? "int64" : type == typeof(decimal) ? "decimal" : type == typeof(DateOnly) ? "date"
        : type == typeof(byte[]) ? "bytes" : type == typeof(JsonNode) ? "json"
        : Nullable.GetUnderlyingType(type) is { } optional ? $"optional<{Primitive(optional)}>"
        : type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>) ? $"list<{Primitive(type.GetGenericArguments()[0])}>"
        : throw new NotSupportedException($"No canonical mapping for {type}.");
}
