using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using Services.Companies.Contracts;
using Services.Companies.Contracts.Services;

namespace Web.Modules;

public sealed class RuntimeServiceLoader(ModuleCatalog catalog, ComponentRegistry components, IServiceStateStore states, ILogger<RuntimeServiceLoader> logger, IConfiguration configuration, IHttpClientFactory clients) : IRuntimeServiceLoader
{
    public async Task<RuntimeServiceLoadResult> BuildAndLoad(string serviceDirectory, CancellationToken cancellationToken = default)
    {
        try
        {
            var project = Directory.EnumerateFiles(serviceDirectory, "*.csproj", SearchOption.TopDirectoryOnly).Single();
            var output = Path.Combine(serviceDirectory, "bin", "Runtime");
            var builderUrl = configuration["RuntimeBuilder:Url"];
            if (!string.IsNullOrWhiteSpace(builderUrl))
            {
                var root = Path.GetFullPath(configuration["RuntimeBuilder:ServicesRoot"] ?? "/app/company/Admin/Services");
                var directory = Path.GetFullPath(serviceDirectory);
                if (!directory.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    return new(false, Error: "The service directory is outside the company boundary.");
                using var request = new HttpRequestMessage(HttpMethod.Post, builderUrl.TrimEnd('/') + "/build")
                {
                    Content = JsonContent.Create(new { RelativePath = Path.GetRelativePath(root, directory) })
                };
                request.Headers.Add("X-CompanyHQ-Builder-Token", configuration["RuntimeBuilder:Token"]);
                using var response = await clients.CreateClient().SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogError("Runtime builder rejected {Directory}: {BuildOutput}", serviceDirectory, await response.Content.ReadAsStringAsync(cancellationToken));
                    return new(false, Error: "The service did not compile. It was skipped and the app remains available.");
                }
            }
            else
            {
                var process = Process.Start(new ProcessStartInfo("dotnet", $"build \"{project}\" -c Release -o \"{output}\" --nologo")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            })!;
            var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
            {
                logger.LogError("Runtime service build failed for {Directory}: {BuildOutput}", serviceDirectory, stdout + stderr);
                return new(false, Error: "The service did not compile. It was skipped and the app remains available.");
            }
            }

            var assemblyPath = Path.Combine(output, Path.GetFileNameWithoutExtension(project) + ".dll");
            var context = new AssemblyLoadContext($"service-{Guid.NewGuid():N}", isCollectible: false);
            context.Resolving += (_, name) => AssemblyLoadContext.Default.Assemblies.FirstOrDefault(candidate => AssemblyName.ReferenceMatchesDefinition(candidate.GetName(), name));
            await using var assemblyStream = File.OpenRead(assemblyPath);
            var assembly = context.LoadFromStream(assemblyStream);
            var moduleType = assembly.GetTypes().Single(type => !type.IsAbstract && typeof(IServiceModule).IsAssignableFrom(type));
            var module = (IServiceModule)Activator.CreateInstance(moduleType)!;
            if (!catalog.AddRuntime(module, assembly, components, logger)) return new(false, Error: $"Service '{module.Manifest.Id}' is already loaded or invalid.");
            await states.Enroll(module.Manifest.Id, true, cancellationToken);
            return new(true, module.Manifest.Id);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Runtime service load failed for {Directory}; the service was skipped.", serviceDirectory);
            return new(false, Error: "The service could not be loaded. It was skipped and the app remains available.");
        }
    }
}
