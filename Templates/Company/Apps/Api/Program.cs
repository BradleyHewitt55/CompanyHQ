using CompanyHQ.Services.Email.Contracts.Ports;
using CompanyHQ.Services.Media.Contracts.Ports;
using System.Text.Json.Serialization;
using Api.Cors;
using Api.Data;
using Api.Integrations;
using Northstar.Demo;
using Api.Validation;
using CompanyHQ.Services.Appointments.Extensions;
using CompanyHQ.Services.Auth.Extensions;
using CompanyHQ.Services.Blogs.Extensions;
using CompanyHQ.Services.Cache.Extensions;
using CompanyHQ.Services.Cloudflare.Extensions;
using CompanyHQ.Services.Company.Extensions;
using CompanyHQ.Services.Contact.Extensions;
using CompanyHQ.Services.Email.Extensions;
using CompanyHQ.Services.Faqs.Extensions;
using CompanyHQ.Services.Firebase.Extensions;
using CompanyHQ.Services.Geo.Extensions;
using CompanyHQ.Services.GitHub.Extensions;
using CompanyHQ.Services.Inbox.Extensions;
using CompanyHQ.Services.Mcp.Extensions;
using CompanyHQ.Services.Media.Extensions;
using CompanyHQ.Services.Messages.Extensions;
using CompanyHQ.Services.Moderation.Extensions;
using CompanyHQ.Services.Notifications.Extensions;
using CompanyHQ.Services.Postgres.Extensions;
using CompanyHQ.Services.Resend.Extensions;
using CompanyHQ.Services.Users.Extensions;
using CompanyHQ.Services.Widget.Extensions;

// Before the host builds, so the .env values are visible to configuration binding.
EnvironmentFile.Load();

var builder = WebApplication.CreateBuilder(args);

// Every service's constructor dependencies are checked when the host builds, in every
// environment — so a port with no adapter (Email without Resend, Media without Cloudflare) stops
// startup instead of failing the first request that needs it.
builder.Host.UseDefaultServiceProvider((context, provider) =>
{
    provider.ValidateOnBuild = true;
    provider.ValidateScopes = context.HostingEnvironment.IsDevelopment();
});

var mvc = builder.Services.AddControllers(options =>
    {
        options.Filters.Add<DefinedEnumQueryFilter>();
        options.Filters.Add<SearchFieldExceptionFilter>();
    })
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false)));

// Host-wide plumbing each service shares rather than registering its own copy.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddRateLimiter(options => options.RejectionStatusCode = StatusCodes.Status429TooManyRequests);

// The marketing site is a separate static origin, so its browser calls need an explicit allow-list.
// Credentials are never allowed: nothing under /api that a marketing page reads is session-scoped.
builder.Services.AddCors(options => options.AddPolicy(
    MarketingCors.PolicyName,
    policy => policy
        .WithOrigins(MarketingCors.Origins(builder.Configuration))
        .WithMethods("GET", "POST", "OPTIONS")
        .WithHeaders("Content-Type")));

// Infrastructure first. Postgres discovers every service's models from this assembly's dependency
// graph and applies the migrations built into it.
builder.Services.AddPostgres(builder.Configuration, System.Reflection.Assembly.GetExecutingAssembly());
builder.Services.AddUsers(builder.Configuration);
builder.Services.AddCache(builder.Configuration);

// JWT bearer authentication plus /api/auth. It verifies passwords with AddUsers' IPasswordHasher.
builder.Services.AddAuth(builder.Configuration);

// Each port is registered alongside the adapter that fills it.
builder.Services.AddEmail();
builder.Services.AddSingleton<IEmailProvider, LocalEmailProvider>();
builder.Services.AddMedia(builder.Configuration);
builder.Services.AddSingleton<LocalObjectStorage>();
builder.Services.AddSingleton<IMediaObjectStorageProvider>(provider => provider.GetRequiredService<LocalObjectStorage>());

builder.Services.AddAppointments();
builder.Services.AddCompany();
builder.Services.AddContact();
builder.Services.AddBlogs();
builder.Services.AddFaqs();
builder.Services.AddGeo(builder.Configuration);
builder.Services.AddGitHub(builder.Configuration);
builder.Services.AddInbox();
builder.Services.AddModeration();
builder.Services.AddFirebase(builder.Configuration);
builder.Services.AddNotifications();
builder.Services.AddMessages(builder.Configuration);
builder.Services.AddWidget(builder.Configuration);

// Feature services never reference each other; the host plugs their ports together here.
builder.Services.AddIntegrations();

// Last: it discovers MCP-marked controller actions from every assembly registered above.
builder.Services.AddMcp(builder.Configuration);

// One document for every controller, generic and company-owned; the website's types come from it.
builder.Services.AddOpenApi("api");

// One contract per company-owned service, discovered from its controllers rather than listed, and
// named after its folder (Company.Services.Orders -> order -> Services/Orders/Contracts/OpenApi).
var controllers = new Microsoft.AspNetCore.Mvc.Controllers.ControllerFeature();
mvc.PartManager.PopulateFeature(controllers);
foreach (var assembly in controllers.Controllers.Select(controller => controller.Assembly).Distinct()
    .Where(assembly => !assembly.GetName().Name!.StartsWith("CompanyHQ.", StringComparison.Ordinal)))
{
    var service = assembly.GetName().Name!.Split('.')[^1].ToLowerInvariant();
    builder.Services.AddOpenApi(service.EndsWith('s') ? service[..^1] : service, options => options.ShouldInclude = description =>
        description.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor action
        && action.ControllerTypeInfo.Assembly == assembly);
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{

}

// Loopback demo uses HTTP; production terminates TLS separately.

app.UseCors(MarketingCors.PolicyName);

// Order matters: authentication establishes who the caller is, authorization then decides.
app.UseAuthentication();
// Keep rate limiting after authentication so report and message policies partition by authenticated sub.
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapMessages();
app.MapMcpEndpoint();
app.MapHealthChecks("/health");
app.MapLocalObjectStorage();

app.Run();

// Exposes the entry point to the black-box API integration tests.
public partial class Program;

