using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Services.Companies.Contracts.Services;

namespace Services.Scaffolding;

public sealed record ScaffoldResult(string Directory, string ServerDirectory, string Slug, string DbPassword, string BootstrapToken, string AdminUrl);

public static class ScaffoldGenerator
{
    private static readonly string[] BlueprintDirectories =
    [
        "CompanyHQ/Admin/Pages", "CompanyHQ/Admin/Services",
        "CompanyHQ/Projects/Website/Pages", "CompanyHQ/Projects/Website/Services",
        "src/Services", "lib", "data", "data-protection"
    ];

    public static ScaffoldResult Generate(string outputRoot, string name, string provider = "postgres")
    {
        var slug = Regex.Replace(string.Concat(name.Trim().ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '-')), "-+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(slug)) throw new ArgumentException("Company name must contain letters or numbers.", nameof(name));
        var root = Path.Combine(outputRoot, slug);
        var outputDirectory = new DirectoryInfo(Path.GetFullPath(outputRoot));
        var serverRoot = outputDirectory.Name.Equals("Companies", StringComparison.OrdinalIgnoreCase)
            ? outputDirectory.Parent?.FullName ?? throw new InvalidOperationException("The companies directory must have a parent server directory.")
            : outputDirectory.FullName;
        Directory.CreateDirectory(root);
        foreach (var path in BlueprintDirectories) Directory.CreateDirectory(Path.Combine(root, path));
        CopySourceTemplate(root);
        var dbPassword = Secret(32);
        var bootstrapToken = Secret(40);
        var builderToken = Secret(40);
        var adminUrl = $"https://{slug}.localhost/admin";
        var company = new { id = Guid.NewGuid(), name, slug, dbProvider = provider, databaseName = "companyhq", deploymentStatus = "running", adminUrl };
        File.WriteAllText(Path.Combine(root, "company.json"), JsonSerializer.Serialize(company, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(root, ".env"), $"COMPANYHQ_DB_PASSWORD={dbPassword}{Environment.NewLine}COMPANYHQ_BOOTSTRAP_TOKEN={bootstrapToken}{Environment.NewLine}COMPANYHQ_BUILDER_TOKEN={builderToken}{Environment.NewLine}COMPANYHQ_BOOTSTRAP_ENABLED=true{Environment.NewLine}COMPANYHQ_SITE=https://localhost{Environment.NewLine}");
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(Path.Combine(root, ".env"), UnixFileMode.UserRead | UnixFileMode.UserWrite);
        var compose = $$"""
            services:
              builder:
                build: { context: ., target: builder }
                restart: unless-stopped
                environment:
                  Builder__Token: ${COMPANYHQ_BUILDER_TOKEN}
                  Builder__ServicesRoot: /app/CompanyHQ/Admin/Services
                volumes: ["./CompanyHQ/Admin/Services:/app/CompanyHQ/Admin/Services", "./lib:/app/lib:ro"]
                logging: { driver: json-file, options: { max-size: "10m", max-file: "3" } }
              app:
                build: .
                restart: unless-stopped
                environment:
                  Database__Provider: postgres
                  ConnectionStrings__Company: Host=db;Database=companyhq;Username=companyhq;Password=${COMPANYHQ_DB_PASSWORD}
                  Company__Id: {{company.id}}
                  Bootstrap__Enabled: ${COMPANYHQ_BOOTSTRAP_ENABLED:-false}
                  Bootstrap__Token: ${COMPANYHQ_BOOTSTRAP_TOKEN}
                  RuntimeBuilder__Url: http://builder:8081
                  RuntimeBuilder__Token: ${COMPANYHQ_BUILDER_TOKEN}
                  RuntimeBuilder__ServicesRoot: /app/CompanyHQ/Admin/Services
                  AdminPagesPath: /app/CompanyHQ/Admin/Pages
                  Scaffolding__Root: /app
                volumes:
                  - ./data-protection:/data-protection
                  - ./CompanyHQ/Admin/Services:/app/CompanyHQ/Admin/Services
                  - ./CompanyHQ/Admin/Pages:/app/CompanyHQ/Admin/Pages
                  - ./CompanyHQ/Projects/Website:/app/CompanyHQ/Projects/Website
                  - ./lib:/app/lib:ro
                depends_on:
                  db: { condition: service_healthy }
                  builder: { condition: service_started }
                healthcheck: { test: ["CMD", "curl", "-fsS", "http://localhost:8080/health"], interval: 10s, timeout: 3s, retries: 5 }
                logging: { driver: json-file, options: { max-size: "10m", max-file: "3" } }
                networks:
                  default: {}
                  companyhq:
                    aliases: ["{{slug}}-app"]
              db:
                image: postgres:17-alpine@sha256:742f40ea20b9ff2ff31db5458d127452988a2164df9e17441e191f3b72252193
                restart: unless-stopped
                environment:
                  POSTGRES_DB: companyhq
                  POSTGRES_USER: companyhq
                  POSTGRES_PASSWORD: ${COMPANYHQ_DB_PASSWORD}
                volumes: ["${COMPANYHQ_DATA_PATH:-./data}:/var/lib/postgresql/data"]
                healthcheck: { test: ["CMD-SHELL", "test \"$(cat /proc/1/comm)\" = postgres && pg_isready -U companyhq -d companyhq"], interval: 2s, timeout: 2s, retries: 45 }
                logging: { driver: json-file, options: { max-size: "10m", max-file: "3" } }
            networks:
              companyhq:
                external: true
                name: companyhq
            """;
        File.WriteAllText(Path.Combine(root, "docker-compose.yml"), compose);
        WriteServerProxy(serverRoot, slug);
        return new ScaffoldResult(root, serverRoot, slug, dbPassword, bootstrapToken, adminUrl);
    }

    private static void CopySourceTemplate(string destination)
    {
        var repository = FindRepositoryRoot();
        CopyTree(Path.Combine(repository, "src", "Web"), Path.Combine(destination, "src", "Web"));
        CopyTree(Path.Combine(repository, "src", "Builder"), Path.Combine(destination, "src", "Builder"));
        foreach (var serviceDirectory in Directory.EnumerateDirectories(Path.Combine(repository, "src", "Services")))
        {
            var manifestPath = Directory.EnumerateFiles(serviceDirectory, "*.json", SearchOption.TopDirectoryOnly).SingleOrDefault();
            if (manifestPath is null) continue;
            var manifest = ServiceManifest.Parse(File.ReadAllText(manifestPath));
            if (!manifest.Hidden)
            {
                CopyTree(serviceDirectory, Path.Combine(destination, "src", "Services", Path.GetFileName(serviceDirectory)));
                continue;
            }
            foreach (var binary in FindServiceBinaries(serviceDirectory)) File.Copy(binary, Path.Combine(destination, "lib", Path.GetFileName(binary)), true);
            File.Copy(manifestPath, Path.Combine(destination, "lib", Path.GetFileName(manifestPath)), true);
            var native = FindNativeSqlite(serviceDirectory);
            if (native is not null) File.Copy(native, Path.Combine(destination, "lib", Path.GetFileName(native)), true);
        }
        File.Copy(Path.Combine(repository, "Directory.Packages.props"), Path.Combine(destination, "Directory.Packages.props"), true);
        File.Copy(Path.Combine(repository, "docker", "Dockerfile"), Path.Combine(destination, "Dockerfile"), true);
        Directory.CreateDirectory(Path.Combine(destination, "docker"));
        File.Copy(Path.Combine(repository, "docker", "entrypoint.sh"), Path.Combine(destination, "docker", "entrypoint.sh"), true);
        foreach (var project in Directory.EnumerateFiles(Path.Combine(destination, "src"), "*.csproj", SearchOption.AllDirectories)) RewriteHiddenReferences(project, destination);
    }

    private static void RewriteHiddenReferences(string project, string destination)
    {
        var content = File.ReadAllText(project);
        foreach (var name in new[] { "Companies", "Auth", "Postgres", "Sqlite", "Scaffolding" })
            content = Regex.Replace(content, $"<ProjectReference Include=\"[^\"]*{name}\\\\{name}\\.csproj\" \\/>",
                $"<Reference Include=\"{name}\"><HintPath>{Path.GetRelativePath(Path.GetDirectoryName(project)!, Path.Combine(destination, "lib", name + ".dll"))}</HintPath></Reference>", RegexOptions.IgnoreCase);
        File.WriteAllText(project, content);
    }

    private static void CopyTree(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        foreach (var directory in Directory.EnumerateDirectories(source).Where(path => !new[] { "bin", "obj" }.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)))
            CopyTree(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }

    private static IEnumerable<string> FindServiceBinaries(string serviceDirectory)
    {
        var assembly = Path.GetFileName(serviceDirectory) + ".dll";
        var output = new[] { "Release", "Debug" }.Select(configuration => Path.Combine(serviceDirectory, "bin", configuration, "net10.0"))
            .FirstOrDefault(path => File.Exists(Path.Combine(path, assembly)));
        return output is null ? [] : Directory.EnumerateFiles(output, "*.dll", SearchOption.TopDirectoryOnly);
    }

    private static string? FindNativeSqlite(string serviceDirectory) => new[] { "Release", "Debug" }
        .Select(configuration => Path.Combine(serviceDirectory, "bin", configuration, "net10.0", "runtimes", "linux-x64", "native", "libe_sqlite3.so"))
        .FirstOrDefault(File.Exists);

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "CompanyHQ.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the CompanyHQ source template.");
    }

    private static string Secret(int bytes) => Convert.ToHexString(RandomNumberGenerator.GetBytes(bytes)).ToLowerInvariant();

    private static void WriteServerProxy(string serverRoot, string slug)
    {
        Directory.CreateDirectory(serverRoot);
        var routes = Path.Combine(serverRoot, "routes");
        Directory.CreateDirectory(routes);
        var composePath = Path.Combine(serverRoot, "docker-compose.yml");
        if (!File.Exists(composePath)) File.WriteAllText(composePath, """
            services:
              proxy:
                image: caddy:2.10-alpine@sha256:4c6e91c6ed0e2fa03efd5b44747b625fec79bc9cd06ac5235a779726618e530d
                restart: unless-stopped
                ports: ["80:80", "443:443"]
                volumes: ["./Caddyfile:/etc/caddy/Caddyfile:ro", "./routes:/etc/caddy/routes:ro", "caddy-data:/data"]
                networks: [companyhq]
                logging: { driver: json-file, options: { max-size: "10m", max-file: "3" } }
            volumes:
              caddy-data:
            networks:
              companyhq:
                name: companyhq
            """ + Environment.NewLine);
        var caddyfilePath = Path.Combine(serverRoot, "Caddyfile");
        if (!File.Exists(caddyfilePath)) File.WriteAllText(caddyfilePath, """
            {
                auto_https disable_redirects
            }
            :80 {
                redir https://{host}{uri} permanent
            }
            import /etc/caddy/routes/*.caddy
            """ + Environment.NewLine);
        File.WriteAllText(Path.Combine(routes, slug + ".caddy"), $"https://{slug}.localhost {{\n    reverse_proxy {slug}-app:8080\n}}\n");
    }
}
