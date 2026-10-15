using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Integrations;
using CompanyHQ.Appointments.Extensions;
using CompanyHQ.Auth.Extensions;
using CompanyHQ.Auth.Services;
using CompanyHQ.Blogs.Contracts.Forms;
using CompanyHQ.Blogs.Extensions;
using CompanyHQ.Blogs.Services;
using CompanyHQ.Cache.Extensions;
using CompanyHQ.Company.Contracts.Forms;
using CompanyHQ.Company.Contracts.Dtos;
using CompanyHQ.Company.Contracts.Models;
using CompanyHQ.Company.Contracts.Ports;
using CompanyHQ.Company.Extensions;
using CompanyHQ.Company.Services;
using CompanyHQ.Contact.Extensions;
using CompanyHQ.Email.Contracts.Enums;
using CompanyHQ.Email.Contracts.Models;
using CompanyHQ.Email.Contracts.Ports;
using CompanyHQ.Email.Extensions;
using CompanyHQ.Email.Services;
using CompanyHQ.Faqs.Extensions;
using CompanyHQ.Geo.Contracts.Ports;
using CompanyHQ.Geo.Extensions;
using CompanyHQ.GitHub.Extensions;
using CompanyHQ.Inbox.Extensions;
using CompanyHQ.Inbox.Services;
using CompanyHQ.Mcp.Contracts.Enums;
using CompanyHQ.Mcp.Extensions;
using CompanyHQ.Mcp.Services;
using CompanyHQ.Media.Contracts.Enums;
using CompanyHQ.Media.Contracts.Models;
using CompanyHQ.Media.Contracts.Ports;
using CompanyHQ.Media.Extensions;
using CompanyHQ.Media.Services;
using CompanyHQ.Messages.Extensions;
using CompanyHQ.Moderation.Extensions;
using CompanyHQ.Notifications.Contracts.Enums;
using CompanyHQ.Notifications.Contracts.Models;
using CompanyHQ.Notifications.Extensions;
using CompanyHQ.Persistence.Services;
using CompanyHQ.Postgres.Contracts;
using CompanyHQ.Postgres.Contracts.Forms;
using CompanyHQ.Postgres.Data;
using CompanyHQ.Postgres.Extensions;
using CompanyHQ.Rss.Extensions;
using CompanyHQ.Sales.Extensions;
using CompanyHQ.Sales.Services;
using CompanyHQ.ServiceCatalog;
using CompanyHQ.ServiceCatalog.Contracts;
using CompanyHQ.Shipping.Extensions;
using CompanyHQ.Users.Contracts.Enums;
using CompanyHQ.Users.Contracts.Models;
using CompanyHQ.Users.Extensions;
using CompanyHQ.Widget.Extensions;
using CompanyHQ.Widget.Services;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Xunit;

namespace CompanyHQ.Tests;

public sealed class ServiceCatalogTests(ServiceCatalogFixture fixture) : IClassFixture<ServiceCatalogFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public async Task AccountDeletionGuardsStillRunWhileTheirServiceIsDisabled()
    {
        var admin = await fixture.ResetAsync();
        var member = await fixture.UserAsync([]);
        await using var scope = fixture.App.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICompanyServiceCatalog>();
        Assert.Equal(ServiceChangeError.None, (await catalog.ChangeAsync("messages", new(false), admin.Id)).Error);
        Assert.Equal(ServiceChangeError.None, (await catalog.ChangeAsync("moderation", new(false), admin.Id)).Error);
        // The Messages guard is a data check, not a Messages operation: switching Messages off must not
        // make every account undeletable (it used to surface as a 404 from the catalog middleware).
        using var client = fixture.Client(admin);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/users/{member.Id}")).StatusCode);
        await catalog.ChangeAsync("moderation", new(true), admin.Id);
        await catalog.ChangeAsync("messages", new(true), admin.Id);
    }

    [Fact]
    public async Task SelectionsPersistBeforeProfileAndReadOnlyCommittedState()
    {
        var admin = await fixture.ResetAsync();
        using var client = fixture.Client(admin);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/company")).StatusCode);
        await using var scope = fixture.App.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICompanyServiceCatalog>();
        var availability = scope.ServiceProvider.GetRequiredService<IServiceAvailability>();
        var all = await catalog.ListAsync(admin.Id);
        Assert.Equal(HqServiceCatalog.AllowedServices.Order(), all.Select(item => item.Descriptor.Key));
        Assert.All(all.Where(item => item.Descriptor.Core), item => Assert.True(item.Enabled));
        Assert.Equal(ServiceStatus.EnabledNotConfigured, (await availability.GetAsync("email"))!.Status);
        Assert.Equal(ServiceChangeError.CoreService, (await catalog.ChangeAsync("email", new(false), admin.Id)).Error);
        Assert.Equal(ServiceChangeError.CoreService, (await catalog.ChangeAsync("rss", new(false), admin.Id)).Error);
        Assert.Equal(ServiceChangeError.RequiredByEnabledService, (await catalog.ChangeAsync("moderation", new(false), admin.Id)).Error);
        Assert.Equal(ServiceChangeError.None, (await catalog.ChangeAsync("messages", new(false), admin.Id)).Error);
        Assert.Equal(ServiceChangeError.None, (await catalog.ChangeAsync("moderation", new(false), admin.Id)).Error);
        Assert.Equal(ServiceChangeError.DependencyUnavailable, (await catalog.ChangeAsync("messages", new(true), admin.Id)).Error);
        Assert.Equal(ServiceChangeError.None, (await catalog.ChangeAsync("moderation", new(true), admin.Id)).Error);
        Assert.Equal(ServiceChangeError.None, (await catalog.ChangeAsync("messages", new(true), admin.Id)).Error);
        Assert.Equal(ServiceChangeError.None, (await catalog.ChangeAsync("blogs", new(false), admin.Id)).Error);
        await using (var fresh = fixture.App.Services.CreateAsyncScope())
            Assert.Equal(ServiceStatus.Disabled, (await fresh.ServiceProvider.GetRequiredService<IServiceAvailability>().GetAsync("blogs"))!.Status);

        // A write saved inside an uncommitted transaction is invisible even to the same caller's availability singleton.
        var db = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        db.ChangeTracker.Clear();
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var state = await db.Set<CompanyServiceState>().SingleAsync();
            state.SelectionsJson = "{}";
            await db.SaveChangesAsync();
            Assert.Equal(ServiceStatus.Disabled, (await availability.GetAsync("blogs"))!.Status);
            await transaction.RollbackAsync();
        });
        db.ChangeTracker.Clear();
        var providerChange = await catalog.ChangeAsync("email", new(true, "localsmtp"), admin.Id);
        Assert.True(providerChange.Service!.RestartRequired);
        Assert.Equal(ServiceStatus.EnabledNotConfigured, providerChange.Service.Status);
        Assert.Equal(ServiceChangeError.UnsafeProviderChange, (await catalog.ChangeAsync("media", new(true, "local"), admin.Id)).Error);
        Assert.Equal(ServiceChangeError.UnknownProvider, (await catalog.ChangeAsync("email", new(true, "sendgrid"), admin.Id)).Error);
        await catalog.ChangeAsync("shipping", new(false), admin.Id);
        var bridgeError = await Assert.ThrowsAsync<ServiceDependencyUnavailableException>(() =>
            scope.ServiceProvider.GetRequiredService<CompanyHQ.Sales.Contracts.Ports.ISalesProfitShipping>().GetMethodsAsync());
        Assert.Equal("shipping", bridgeError.DependencyKey);
        await catalog.ChangeAsync("shipping", new(true), admin.Id);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/shipping/addresses/search?query=fixture")).StatusCode);
        Assert.Equal(0, await db.Set<CompanyProfile>().CountAsync());
        var staff = await fixture.UserAsync([AccessPermissions.ManageSales]);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => catalog.ChangeAsync("sales", new(false), staff.Id));
    }

    [Theory]
    [InlineData(AccessPermissions.ManageSales, "sales", "/sales")]
    [InlineData(AccessPermissions.ManageBlogs, "blogs", "/blog")]
    public async Task NavigationUsesCurrentRoleWithoutManagementPrivilege(string permission, string key, string destination)
    {
        var admin = await fixture.ResetAsync();
        var staff = await fixture.UserAsync([permission]);
        using var client = fixture.Client(staff);
        var response = await client.GetAsync("/api/company/navigation");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        var entry = Assert.Single(JsonSerializer.Deserialize<ServiceNavigationDto[]>(text, Json)!);
        Assert.Equal((key, destination), (entry.Key, entry.Destination));
        Assert.DoesNotContain("provider", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("configuration", text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/company/services")).StatusCode);
        await using var scope = fixture.App.Services.CreateAsyncScope();
        var nav = scope.ServiceProvider.GetRequiredService<ICompanyServiceNavigation>();
        var blogs = scope.ServiceProvider.GetRequiredService<IBlogService>();
        var sales = scope.ServiceProvider.GetRequiredService<SalesCatalogService>();
        if (key == "blogs")
        {
            var draft = await blogs.CreateAsync(new("staff-" + Guid.NewGuid().ToString("N"), "Staff article", "A sufficiently long description.", "A sufficiently long article body.", "Staff", "Updates"), staff.Id);
            Assert.NotNull(draft);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/blog")).StatusCode);
        }
        else Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/sales/suppliers")).StatusCode);
        Assert.Equal(ServiceChangeError.None, (await scope.ServiceProvider.GetRequiredService<ICompanyServiceCatalog>().ChangeAsync(key, new(false), admin.Id)).Error);
        Assert.Empty(await nav.ListAsync(staff.Id));
        Assert.Empty((await client.GetFromJsonAsync<ServiceNavigationDto[]>("/api/company/navigation", Json))!);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(key == "sales" ? "/api/sales/products" : "/api/blog")).StatusCode);
        var denied = key == "sales"
            ? await Assert.ThrowsAsync<ServiceUnavailableException>(() => sales.CategoriesAsync(new(), new()))
            : await Assert.ThrowsAsync<ServiceUnavailableException>(() => blogs.ListAsync(new()));
        Assert.Equal(ServiceStatus.Disabled, denied.Status);
        Assert.Equal(ServiceChangeError.None, (await scope.ServiceProvider.GetRequiredService<ICompanyServiceCatalog>().ChangeAsync(key, new(true), admin.Id)).Error);
        Assert.Single(await nav.ListAsync(staff.Id));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(key == "sales" ? "/api/sales/products" : "/api/blog")).StatusCode);
        await using (var edit = fixture.App.Services.CreateAsyncScope())
        {
            var db = edit.ServiceProvider.GetRequiredService<PostgresDbContext>();
            var role = await db.Set<AccessRole>().SingleAsync(row => row.Id == staff.AccessRoleId);
            role.Permissions = [];
            await db.SaveChangesAsync();
        }
        // Same JWT and same resolved navigation port: both must read current stored authority.
        Assert.Empty(await nav.ListAsync(staff.Id));
        Assert.Empty((await client.GetFromJsonAsync<ServiceNavigationDto[]>("/api/company/navigation", Json))!);
        if (key == "sales") Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/sales/suppliers")).StatusCode);
        else await Assert.ThrowsAsync<UnauthorizedAccessException>(() => blogs.CreateAsync(new("revoked-"+Guid.NewGuid().ToString("N"), "Staff article", "A sufficiently long description.", "A sufficiently long article body.", "Staff", "Updates"), staff.Id));
    }

    [Fact]
    public async Task HttpAndMcpRespectOwnershipAndExistingNarrowingRules()
    {
        var admin = await fixture.ResetAsync();
        await using var scope = fixture.App.Services.CreateAsyncScope();
        var keys = scope.ServiceProvider.GetRequiredService<IAgentKeyService>();
        var issued = await keys.IssueAsync(admin.Id, new("Readonly fixture", AgentKeyAccess.ReadOnly, McpDisabledTools: [McpCapabilityCatalog.ListProducts]));
        Assert.True(issued.Succeeded);
        using var agent = fixture.Agent(issued.Key!.Key);
        var listed = await Rpc(agent, "tools/list", new { });
        var names = listed.GetProperty("result").GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("name").GetString()).ToArray();
        Assert.Contains(McpCapabilityCatalog.ListBlogPosts, names);
        Assert.DoesNotContain(McpCapabilityCatalog.CreateBlogPost, names);
        Assert.DoesNotContain(McpCapabilityCatalog.ListProducts, names);
        Assert.True((await Rpc(agent, "tools/call", new { name = McpCapabilityCatalog.CreateBlogPost, arguments = new { } })).TryGetProperty("error", out _));
        Assert.True((await Rpc(agent, "tools/call", new { name = McpCapabilityCatalog.ListProducts, arguments = new { } })).TryGetProperty("error", out _));
        var catalog = scope.ServiceProvider.GetRequiredService<ICompanyServiceCatalog>();
        await catalog.ChangeAsync("blogs", new(false), admin.Id);
        listed = await Rpc(agent, "tools/list", new { });
        Assert.DoesNotContain(McpCapabilityCatalog.ListBlogPosts, listed.GetProperty("result").GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("name").GetString()));
        Assert.True((await Rpc(agent, "tools/call", new { name = McpCapabilityCatalog.ListBlogPosts, arguments = new { } })).TryGetProperty("error", out _));
        using var anonymous = fixture.Client();
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/blog")).StatusCode);
        await catalog.ChangeAsync("blogs", new(true), admin.Id);
        var narrowed = await fixture.UserAsync([McpCapabilityCatalog.ListBlogPosts]);
        var inherited = await keys.IssueAsync(admin.Id, new("Narrow fixture", AgentKeyAccess.Write, AccessRoleId: narrowed.AccessRoleId));
        using var limited = fixture.Agent(inherited.Key!.Key);
        Assert.Equal([McpCapabilityCatalog.ListBlogPosts], (await Rpc(limited, "tools/list", new { })).GetProperty("result").GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("name").GetString()));
        await catalog.ChangeAsync("messages", new(false), admin.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.PostAsync("/hubs/messages/negotiate?negotiateVersion=1", null)).StatusCode);
        await catalog.ChangeAsync("mcp", new(false), admin.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await agent.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 1, method = "tools/list" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/.well-known/oauth-authorization-server")).StatusCode);
    }

    [Fact]
    public async Task WorkersPauseWithoutConsumingAttemptsAndResume()
    {
        var admin = await fixture.ResetAsync();
        await using var scope = fixture.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var due = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 60);
        var emails = Enumerable.Range(0, 2).Select(index => new QueuedEmail { To = ["worker@example.test"], Subject = "Fixture", Html = "<p>Fixture body</p>", Text = "Fixture body", IdempotencyKey = Guid.NewGuid().ToString(), Attempts = 3, NextAttemptAt = due }).ToArray();
        var notification = new QueuedNotification { RecipientUserId = admin.Id, Kind = "fixture", Title = "Fixture", Body = "Fixture", Channels = NotificationChannels.Push, Attempts = 2, NextAttemptAt = due };
        var media = new MediaObject { OwnerUserId = admin.Id, Key = "fixture/cleanup", ContentType = "image/png", Status = MediaStatus.Deleted, CleanupFailureCount = 2 };
        db.AddRange(emails); db.Add(notification); db.Add(media); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Assert.Equal(0, await fixture.ProcessAsync(scope.ServiceProvider, typeof(EmailService).Assembly, "CompanyHQ.Email.Services.EmailQueueProcessor", "ProcessBatchAsync"));
        Assert.Equal(0, await fixture.ProcessAsync(scope.ServiceProvider, typeof(MediaService).Assembly, "CompanyHQ.Media.Services.MediaCleanupProcessor", "CleanupBatchAsync"));
        Assert.Equal(0, await fixture.ProcessAsync(scope.ServiceProvider, typeof(CompanyHQ.Notifications.Services.NotificationService).Assembly, "CompanyHQ.Notifications.Services.NotificationQueueProcessor", "ProcessBatchAsync"));
        Assert.All(await db.Set<QueuedEmail>().Where(row => emails.Select(item => item.Id).Contains(row.Id)).ToArrayAsync(), row => { Assert.Equal(3, row.Attempts); Assert.Equal(due, row.NextAttemptAt); Assert.Equal("<p>Fixture body</p>", row.Html); });
        Assert.Equal(2, (await db.Set<QueuedNotification>().SingleAsync(row => row.Id == notification.Id)).Attempts);
        Assert.Equal(2, (await db.Set<MediaObject>().SingleAsync(row => row.Id == media.Id)).CleanupFailureCount);
        fixture.EmailReady = true;
        fixture.Email.AfterSend = () => fixture.EmailReady = false;
        await fixture.ProcessAsync(scope.ServiceProvider, typeof(EmailService).Assembly, "CompanyHQ.Email.Services.EmailQueueProcessor", "ProcessBatchAsync");
        Assert.Equal(1, fixture.Email.Calls);
        db.ChangeTracker.Clear();
        var remaining = Assert.Single(await db.Set<QueuedEmail>().Where(row => row.Status == QueuedEmailStatus.Pending).ToArrayAsync());
        Assert.Equal(3, remaining.Attempts); Assert.Equal("<p>Fixture body</p>", remaining.Html);
        fixture.Email.AfterSend = null; fixture.EmailReady = true;
        await fixture.ProcessAsync(scope.ServiceProvider, typeof(EmailService).Assembly, "CompanyHQ.Email.Services.EmailQueueProcessor", "ProcessBatchAsync");
        Assert.Equal(2, fixture.Email.Calls);
        fixture.StorageReady = true;
        fixture.Storage.AfterDelete = () => fixture.StorageReady = false;
        Assert.Equal(1, await fixture.ProcessAsync(scope.ServiceProvider, typeof(MediaService).Assembly, "CompanyHQ.Media.Services.MediaCleanupProcessor", "CleanupBatchAsync"));
        Assert.Equal(1, fixture.Storage.Deletes);
        var catalog = scope.ServiceProvider.GetRequiredService<ICompanyServiceCatalog>();
        await catalog.ChangeAsync("notifications", new(false), admin.Id); fixture.PushReady = true;
        Assert.Equal(0, await fixture.ProcessAsync(scope.ServiceProvider, typeof(CompanyHQ.Notifications.Services.NotificationService).Assembly, "CompanyHQ.Notifications.Services.NotificationQueueProcessor", "ProcessBatchAsync"));
        await catalog.ChangeAsync("notifications", new(true), admin.Id);
        Assert.Equal(1, await fixture.ProcessAsync(scope.ServiceProvider, typeof(CompanyHQ.Notifications.Services.NotificationService).Assembly, "CompanyHQ.Notifications.Services.NotificationQueueProcessor", "ProcessBatchAsync"));
        db.ChangeTracker.Clear();
        var delivered = await db.Set<QueuedNotification>().SingleAsync(row => row.Id == notification.Id);
        Assert.Equal(QueuedNotificationStatus.Sent, delivered.Status); Assert.Equal(2, delivered.Attempts);
        await catalog.ChangeAsync("sales", new(false), admin.Id);
        await Assert.ThrowsAsync<ServiceUnavailableException>(() => fixture.ProcessAsync(scope.ServiceProvider, typeof(SalesCatalogService).Assembly, "CompanyHQ.Sales.Services.SalesCatalogService", "EnsurePriceStateAsync"));
        await catalog.ChangeAsync("inbox", new(false), admin.Id);
        await Assert.ThrowsAsync<ServiceUnavailableException>(() => scope.ServiceProvider.GetRequiredService<IVisitorConversationService>().PurgeExpiredAsync());
        var widget = await scope.ServiceProvider.GetRequiredService<IWidgetService>().GetAsync();
        Assert.False(widget.Messages); Assert.False(widget.Issues);
    }

    [Fact]
    public async Task OptInUnconfiguredBootPreservesLegacyStrictRegistrations()
    {
        foreach (var service in new[] { "email", "media", "geo" })
        {
            var builder = fixture.Builder();
            fixture.AddFoundation(builder.Services);
            if (service == "email") builder.Services.AddEmail(builder.Configuration, allowUnconfigured: true);
            if (service == "media") builder.Services.AddMedia(builder.Configuration, allowUnconfigured: true);
            if (service == "geo") builder.Services.AddGeo(builder.Configuration, allowUnconfigured: true);
            await using var provider = builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
            await using var scope = provider.CreateAsyncScope();
            if (service == "email") await Assert.ThrowsAsync<ServiceUnavailableException>(() => scope.ServiceProvider.GetRequiredService<IEmailProvider>().SendAsync(new(["test@example.test"], "Test", "<p>Test</p>", "Test", "fixture")));
            if (service == "media") await Assert.ThrowsAsync<ServiceUnavailableException>(() => scope.ServiceProvider.GetRequiredService<IMediaObjectStorageProvider>().DeleteAsync("fixture"));
            if (service == "geo") await Assert.ThrowsAsync<ServiceUnavailableException>(() => scope.ServiceProvider.GetRequiredService<IGeoProvider>().SearchAreasAsync(new("Fixture", 1, "en"), default));
            var strict = fixture.Builder(); fixture.AddFoundation(strict.Services);
            if (service == "email") strict.Services.AddEmail(strict.Configuration);
            if (service == "media") strict.Services.AddMedia(strict.Configuration);
            if (service == "geo") strict.Services.AddGeo(strict.Configuration);
            Assert.Throws<AggregateException>(() => strict.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true }));
        }
        var malformed = fixture.Builder();
        malformed.Configuration["Firebase:ProjectId"] = "fixture-project";
        malformed.Configuration["Firebase:ServiceAccountJson"] = "{\"project_id\":\"fixture-project\"}";
        Assert.Throws<InvalidOperationException>(() => malformed.Services.AddNotifications(malformed.Configuration, allowUnconfigured: true));
        malformed.Services.AddNotifications(malformed.Configuration, allowUnconfigured: true, useFirebase: false);
        malformed.Configuration["GitHub:TokenEncryptionKey"] = "invalid";
        Assert.Throws<InvalidOperationException>(() => malformed.Services.AddGitHub(malformed.Configuration));
    }

    private static async Task<JsonElement> Rpc(HttpClient client, string method, object parameters)
    {
        using var response = await client.PostAsJsonAsync("/mcp", new { jsonrpc = "2.0", id = 1, method, @params = parameters });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"MCP {method}: {response.StatusCode}: {body}");
        if (body.StartsWith("event:", StringComparison.Ordinal) || body.StartsWith("data:", StringComparison.Ordinal))
            body = body.Split('\n').Last(line => line.StartsWith("data:", StringComparison.Ordinal))[5..].Trim();
        return JsonDocument.Parse(body).RootElement.Clone();
    }
}

public sealed class ServiceCatalogFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder().WithImage("postgis/postgis:17-3.5").Build();
    private readonly IContainer redis = new ContainerBuilder().WithImage("redis:7-alpine").WithPortBinding(6379, true).Build();
    private Dictionary<string, string?> settings = null!;
    public WebApplication App { get; private set; } = null!;
    public bool EmailReady, StorageReady, PushReady;
    public CatalogEmailProvider Email { get; } = new();
    public CatalogStorageProvider Storage { get; } = new();
    private string address = null!;
    public WebApplicationBuilder Builder()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Configuration.Sources.Clear(); builder.Configuration.AddInMemoryCollection(settings);
        builder.Logging.ClearProviders();
        return builder;
    }
    public void AddFoundation(IServiceCollection services)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        services.AddSingleton(TimeProvider.System);
        services.AddPostgres(configuration, typeof(Program).Assembly);
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(configuration["Cache:ConnectionString"]!));
        services.AddCache(configuration);
    }
    public async Task InitializeAsync()
    {
        await postgres.StartAsync(); await redis.StartAsync();
        settings = new() { ["Postgres:ConnectionString"] = postgres.GetConnectionString(), ["Postgres:AutoMigrate"] = "false", ["Postgres:AutoSeed"] = "false", ["Cache:ConnectionString"] = $"{redis.Hostname}:{redis.GetMappedPublicPort(6379)}", ["Email:From"] = "Fixture <fixture@example.test>", ["Media:Bucket"] = "fixture", ["Media:ContentBaseUrl"] = "http://localhost:4002", ["Jwt:SigningKey"] = "fixture-only-signing-key-at-least-thirty-two-characters", ["Jwt:Issuer"] = "fixture", ["Jwt:Audience"] = "fixture", ["Auth:WebBaseAddress"] = "https://example.test" };
        var builder = Builder(); builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Host.UseDefaultServiceProvider(options => { options.ValidateOnBuild = true; options.ValidateScopes = true; });
        var services = builder.Services; AddFoundation(services);
        services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddRateLimiter(options => options.RejectionStatusCode = 429);
        services.AddUsers(builder.Configuration); services.AddAuth(builder.Configuration);
        services.AddEmail<CatalogEmailProvider>(builder.Configuration); services.AddScoped<IEmailProvider>(_ => Email);
        services.AddMedia<CatalogStorageProvider>(builder.Configuration); services.AddSingleton<IMediaObjectStorageProvider>(Storage);
        services.AddSales(builder.Configuration); services.AddShipping(); services.AddAppointments(); services.AddCompany(); services.AddCompanyServiceManagement();
        services.AddContact(); services.AddBlogs(); services.AddFaqs(); services.AddGeo(builder.Configuration, true); services.AddGitHub(builder.Configuration); services.AddInbox(); services.AddModeration(); services.AddNotifications(builder.Configuration, true); services.AddMessages(builder.Configuration); services.AddWidget(builder.Configuration); services.AddRss(builder.Configuration); services.AddIntegrations();
        services.AddHqServiceDescriptors(builder.Configuration);
        foreach (var key in new[] { "email", "media", "notifications" })
        {
            var old = services.Single(item => item.ServiceType == typeof(ServiceRegistration) && ((ServiceRegistration)item.ImplementationInstance!).Descriptor.Key == key);
            services.Remove(old); var registration = (ServiceRegistration)old.ImplementationInstance!;
            services.AddSingleton(registration with { IsConfigured = _ => key == "email" ? EmailReady : key == "media" ? StorageReady : PushReady });
        }
        services.AddMcp(builder.Configuration); services.AddServiceCatalog(HqServiceCatalog.AllowedServices);
        // Processors are invoked deterministically; no timer or vendor call runs in this fixture.
        foreach (var hosted in services.Where(item => item.ServiceType == typeof(IHostedService) && item.ImplementationType?.Assembly.GetName().Name?.StartsWith("CompanyHQ.") == true).ToArray()) services.Remove(hosted);
        App = builder.Build(); App.UseRouting(); App.UseServiceCatalog(); App.UseAuthentication(); App.UseRateLimiter(); App.UseAuthorization(); App.MapControllers(); App.MapMessages(); App.MapMcpEndpoint();
        await using (var scope = App.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<PostgresDbContext>().Database.MigrateAsync();
        await App.StartAsync(); address = App.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    }
    public async Task<User> ResetAsync()
    {
        EmailReady = StorageReady = PushReady = false; Email.Calls = 0; Email.AfterSend = null; Storage.Deletes = 0; Storage.AfterDelete = null;
        await using var scope = App.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        db.RemoveRange(await db.Set<CompanyServiceState>().ToArrayAsync()); db.RemoveRange(await db.Set<QueuedEmail>().ToArrayAsync()); db.RemoveRange(await db.Set<QueuedNotification>().ToArrayAsync()); db.RemoveRange(await db.Set<MediaObject>().ToArrayAsync()); await db.SaveChangesAsync();
        return await UserAsync(null);
    }
    public async Task<User> UserAsync(string[]? permissions)
    {
        await using var scope = App.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var role = permissions is null ? null : new AccessRole { Name = "Fixture " + Guid.NewGuid(), Permissions = permissions };
        if (role is not null) db.Add(role);
        var user = new User { Email = Guid.NewGuid()+"@example.test", PasswordHash = "fixture-only", EmailVerified = true, Role = permissions is null ? UserRole.Admin : UserRole.Client, AccessRole = role };
        db.Add(user); await db.SaveChangesAsync(); return user;
    }
    public HttpClient Client(User? user = null)
    {
        var client = new HttpClient { BaseAddress = new(address), Timeout = TimeSpan.FromSeconds(30) };
        if (user is not null)
        {
            using var scope = App.Services.CreateScope();
            client.DefaultRequestHeaders.Authorization = new("Bearer", scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(user).Value);
        }
        return client;
    }
    public HttpClient Agent(string key)
    {
        var client = Client(); client.DefaultRequestHeaders.Authorization = new("Bearer", key);
        client.DefaultRequestHeaders.Accept.Add(new("application/json")); client.DefaultRequestHeaders.Accept.Add(new("text/event-stream")); return client;
    }
    public async Task<int> ProcessAsync(IServiceProvider provider, Assembly assembly, string typeName, string methodName)
    {
        var type = assembly.GetType(typeName, true)!; var instance = provider.GetRequiredService(type);
        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
        var task = (Task)method.Invoke(instance, [CancellationToken.None])!; await task;
        return task.GetType().GetProperty("Result")?.GetValue(task) is int result ? result : 0;
    }
    public async Task DisposeAsync()
    {
        if (App is not null) await App.DisposeAsync(); await redis.DisposeAsync(); await postgres.DisposeAsync();
    }
}

public sealed class CatalogEmailProvider : IEmailProvider
{
    public int Calls; public Action? AfterSend;
    public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    { Calls++; AfterSend?.Invoke(); return Task.FromResult(EmailSendResult.Sent("fixture")); }
}
public sealed class CatalogStorageProvider : IMediaObjectStorageProvider
{
    public int Deletes;
    public Action? AfterDelete;
    public string CreateUploadUrl(string key, string type, DateTimeOffset expires) => throw new NotSupportedException();
    public string CreateDownloadUrl(string key, DateTimeOffset expires) => throw new NotSupportedException();
    public Task<bool> PutAsync(string key, string type, Stream stream, long length, CancellationToken token = default) => throw new NotSupportedException();
    public Task<StoredObjectMetadata?> GetMetadataAsync(string key, CancellationToken token = default) => throw new NotSupportedException();
    public Task<StorageDeleteOutcome> DeleteAsync(string key, CancellationToken token = default)
    { Deletes++; AfterDelete?.Invoke(); return Task.FromResult(StorageDeleteOutcome.AlreadyGone); }
}
