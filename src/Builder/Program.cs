using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var root = Path.GetFullPath(builder.Configuration["Builder:ServicesRoot"] ?? "/app/company/Admin/Services");
var token = builder.Configuration["Builder:Token"] ?? throw new InvalidOperationException("Builder token is required.");

app.MapPost("/build", async (HttpRequest request, BuildRequest input, CancellationToken cancellationToken) =>
{
    if (!request.Headers.TryGetValue("X-CompanyHQ-Builder-Token", out var supplied) || supplied != token)
        return Results.NotFound();

    var directory = Path.GetFullPath(Path.Combine(root, input.RelativePath));
    if (!directory.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !Directory.Exists(directory))
        return Results.NotFound();
    var projects = Directory.GetFiles(directory, "*.csproj", SearchOption.TopDirectoryOnly);
    if (projects.Length != 1) return Results.BadRequest(new { error = "Service must contain exactly one project." });

    var output = Path.Combine(directory, "bin", "Runtime");
    var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
    foreach (var argument in new[] { "build", projects[0], "-c", "Release", "-o", output, "--nologo" }) start.ArgumentList.Add(argument);
    using var process = Process.Start(start)!;
    var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
    var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
    await process.WaitForExitAsync(cancellationToken);
    return process.ExitCode == 0 ? Results.Ok() : Results.BadRequest(new { error = stdout + stderr });
});

app.Run();
public sealed record BuildRequest(string RelativePath);
