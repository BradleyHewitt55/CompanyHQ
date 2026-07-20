namespace CompanyHQ.Services.Companies.Services;

// The generated service is a compilable ICompanyHqPlugin laid out exactly like
// Projects/Services/Auth, so a scaffolded service and a first-party one are the same
// shape and a developer only ever learns one structure.
internal static class ServiceTemplate
{
    // Directories with no starter file. They exist so the layout is complete the moment
    // the developer needs them rather than being invented per service.
    internal static readonly string[] EmptyDirectories = ["Dtos", "Layout", "Models"];

    internal static string Project(string name) =>
        $"""
        <Project Sdk="Microsoft.NET.Sdk.Razor">

          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <!-- Keeps generated Razor types in the same namespace as the C#; without it
                 they land in {name}.* and every consumer has to import both. -->
            <RootNamespace>CompanyHQ.Services.{name}</RootNamespace>
            <AddRazorSupportForMvc>true</AddRazorSupportForMvc>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <NoWarn>$(NoWarn);NU1510</NoWarn>
          </PropertyGroup>

          <ItemGroup>
            <FrameworkReference Include="Microsoft.AspNetCore.App" />
            <!-- CompanyHqAbstractionsPath comes from Directory.Build.props at the Services
                 root, so relocating the platform is one edit per company, not one per service. -->
            <ProjectReference Include="$(CompanyHqAbstractionsPath)" />
          </ItemGroup>

        </Project>

        """;

    // MSBuild resolves a relative ProjectReference against the .csproj, which sits one
    // level below the Directory.Build.props this value is declared in.
    internal static string DefaultAbstractionsPath { get; } =
        Path.Combine("..", "..", "..", "..", "..", "Projects", "Abstractions", "Abstractions.csproj");

    internal static string BuildProperties(string abstractionsPath) =>
        $"""
        <Project>

          <PropertyGroup>
            <!-- Every scaffolded service in this company compiles against this Abstractions
                 project. Update the path here after moving the CompanyHQ checkout. -->
            <CompanyHqAbstractionsPath>{abstractionsPath}</CompanyHqAbstractionsPath>
          </PropertyGroup>

        </Project>

        """;

    internal static string Plugin(string name) =>
        $$"""
        using System.Reflection;
        using Abstractions.Plugins;
        using Microsoft.AspNetCore.Builder;
        using Microsoft.Extensions.Configuration;
        using Microsoft.Extensions.DependencyInjection;

        namespace CompanyHQ.Services.{{name}};

        public sealed class {{name}}Plugin : ICompanyHqPlugin
        {
            public PluginDescriptor Descriptor { get; } = new(
                "companyhq.{{name.ToLowerInvariant()}}",
                "{{name}}",
                "{{name}} company service.",
                new Version(0, 1, 0),
                PluginOrigin.Company,
                PluginActivation.Optional,
                []);

            public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
            {
                services.AddScoped<Services.{{name}}Service>();
                services.AddControllersWithViews().AddApplicationPart(typeof({{name}}Plugin).Assembly);
            }

            // Pages in this assembly are only routable once the host is told about it.
            public IEnumerable<Assembly> RouteAssemblies => [typeof({{name}}Plugin).Assembly];
        }

        """;

    internal static string Imports(string name) =>
        $"""
        @using Abstractions.Companies
        @using CompanyHQ.Services.{name}
        @using CompanyHQ.Services.{name}.Components
        @using CompanyHQ.Services.{name}.Services
        @using Microsoft.AspNetCore.Components
        @using Microsoft.AspNetCore.Components.Forms
        @using Microsoft.AspNetCore.Components.Web
        @using Microsoft.AspNetCore.Http

        """;

    internal static string Controller(string name) =>
        $$"""
        using Microsoft.AspNetCore.Authorization;
        using Microsoft.AspNetCore.Mvc;

        namespace CompanyHQ.Services.{{name}}.Api;

        [ApiController]
        [Route("api/{{name.ToLowerInvariant()}}")]
        public sealed class {{name}}Controller(Services.{{name}}Service service) : ControllerBase
        {
            [HttpGet("status")]
            [Authorize]
            public IActionResult Status() => Ok(new { service = service.Describe() });
        }

        """;

    internal static string Service(string name) =>
        $$"""
        namespace CompanyHQ.Services.{{name}}.Services;

        // Headless by construction: no host, page, or transport type appears here, so the
        // same class serves the admin UI, the API, and the CLI.
        public sealed class {{name}}Service
        {
            public string Describe() => "{{name}} is scaffolded but not implemented.";
        }

        """;

    internal static string Page(string name) =>
        $$"""
        @page "/{{name.ToLowerInvariant()}}"
        @inject {{name}}Service Service

        <PageTitle>{{name}}</PageTitle>

        <h1>{{name}}</h1>

        <{{name}}Panel Message="@Service.Describe()" />

        """;

    internal static string Panel(string name) =>
        $$"""
        <section class="{{name.ToLowerInvariant()}}-panel">
            <p>@Message</p>
        </section>

        @code {
            [Parameter, EditorRequired] public string Message { get; set; } = default!;
        }

        """;

    internal static string Settings(string name) =>
        $$"""
        {
          "name": "{{name}}",
          "version": "0.1.0",
          "enabled": false
        }

        """;

    internal static string Readme(string name) =>
        $"""
        # {name}

        Scaffolded CompanyHQ service. Implement `Services/{name}Service.cs`, then flip
        `enabled` in `Settings/service.json` once the service is ready to load.

        | Path | Purpose |
        | --- | --- |
        | `Api/` | HTTP controllers exposing this service |
        | `Components/` | Reusable Razor components |
        | `Dtos/` | Transport shapes crossing the service boundary |
        | `Layout/` | Layouts owned by this service |
        | `Models/` | Form and view models |
        | `Pages/` | Routable pages |
        | `Services/` | Headless implementation |
        | `Settings/` | Service manifest |

        """;
}
