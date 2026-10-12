using System.Text.Json;
using System.Text.Json.Nodes;

namespace UniversalApi;

internal static class NativeSdkVerification
{
    public static async Task RunAsync(RpcClient transport)
    {
        var sdk = new DbClient(transport);
        var contract = new NativeRegistration(new UserController(sdk)).Contract;
        var registered = await transport.CallAsync(new JsonObject { ["op"] = "MODEL_REGISTER", ["model"] = "User", ["fields"] = contract["models"]!["User"]!.DeepClone() });
        Check((bool?)registered["result"] == true);
        var saved = await sdk.SaveAsync<User>(new JsonObject { ["name"] = "SDK fixture", ["email"] = "sdk@example.test" });
        var user = saved["result"]!.Deserialize<User>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Check(user.Id.Version == 7 && user.CreatedAt != default);
        var read = await sdk.GetAsync<User>(user.Id);
        Check(JsonNode.DeepEquals(saved, read));
        var updated = await sdk.UpdateAsync(user with { Name = "Updated SDK fixture" });
        Check((string?)updated["result"]?["name"] == "Updated SDK fixture");
        var query = new JsonObject { ["where"] = new JsonObject { ["op"] = "eq", ["field"] = "id", ["value"] = user.Id.ToString() } };
        Check((await sdk.FindAsync<User>(query))["result"]!.AsArray().Count == 1);
        Check((bool?)(await sdk.ExistsAsync<User>(query))["result"] == true);
        Check((int?)(await sdk.CountAsync<User>(query))["result"] == 1);
        Check((bool?)(await sdk.DeleteAsync<User>(user.Id))["result"] == true);
        Check((int?)(await sdk.GetAsync<User>(user.Id))["status"] == 404);
        Console.WriteLine("PASS: native C# DB SDK registration/CRUD/query");
    }
    private static void Check(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Native SDK acceptance failed.");
    }
}
