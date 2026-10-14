using System.Text.Json.Serialization;
using Api.Cors;
using Api.Data;
using Api.Integrations;
using Api.OpenApi;
using Amazon.Runtime;
using Amazon.S3;
using Api.Validation;
using CompanyHQ.Services.Appointments.Extensions;
using CompanyHQ.Services.Auth.Extensions;
using CompanyHQ.Services.Blogs.Extensions;
using CompanyHQ.Services.Cache.Contracts.Models;
using CompanyHQ.Services.Cache.Extensions;
using CompanyHQ.Services.Company.Extensions;
using CompanyHQ.Services.Contact.Extensions;
using CompanyHQ.Services.Email.Extensions;
using CompanyHQ.Services.Faqs.Extensions;
using CompanyHQ.Services.Geo.Extensions;
using CompanyHQ.Services.GitHub.Extensions;
using CompanyHQ.Services.Inbox.Extensions;
using CompanyHQ.Services.Mcp.Extensions;
using CompanyHQ.Services.Media.Extensions;
using CompanyHQ.Services.Messages.Extensions;
using CompanyHQ.Services.Moderation.Extensions;
using CompanyHQ.Services.Notifications.Extensions;
using CompanyHQ.Services.OpenStreetMap.Extensions;
using CompanyHQ.Services.Postgres.Extensions;
using CompanyHQ.Services.Rss.Extensions;
using CompanyHQ.Services.Users.Extensions;
using CompanyHQ.Services.Widget.Extensions;
using CompanyHQ.Services.Sales.Extensions;
using CompanyHQ.Services.Shipping.Extensions;
using Resend;
using StackExchange.Redis;

// Before the host builds, so the .env values are visible to configuration binding.
EnvironmentFile.Load();

var builder = WebApplication.CreateBuilder(args);

// Every service's constructor dependencies are checked when the host builds, in every
// environment — so a missing vendor client (Resend for Email, Redis for Cache, S3 for Media) stops
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
builder.Services.AddPostgres(builder.Configuration, typeof(Program).Assembly);
builder.Services.AddUsers(builder.Configuration);

// Cache stores through the official StackExchange.Redis client, which the host registers first.
// Connecting lazily keeps a cold Redis from blocking startup; AbortOnConnectFail is off, so the
// multiplexer reconnects on its own once Redis is up instead of throwing for every caller.
var cache = builder.Configuration.GetSection(CacheOptions.SectionName).Get<CacheOptions>() ?? new();
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var redis = ConfigurationOptions.Parse(cache.ConnectionString);
    redis.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(redis);
});
builder.Services.AddCache(builder.Configuration);

// JWT bearer authentication plus /api/auth. It verifies passwords with AddUsers' IPasswordHasher.
builder.Services.AddAuth(builder.Configuration);

// Email delivers through the official Resend client, which the host registers first. A missing
// key throws here rather than on the first send.
builder.Services.AddResend(builder.Configuration["Resend:ApiKey"]!);
builder.Services.AddEmail(builder.Configuration);

// Media stores objects through the official AWS S3 client, pointed here at Cloudflare R2. R2 has
// one region, is addressed by path, and rejects the checksum trailers the SDK adds by default.
var r2 = builder.Configuration.GetSection("Cloudflare:R2");
builder.Services.AddSingleton<IAmazonS3>(new AmazonS3Client(
    new BasicAWSCredentials(Required(r2, "AccessKeyId"), Required(r2, "SecretAccessKey")),
    new AmazonS3Config
    {
        ServiceURL = $"https://{Required(r2, "AccountId")}.r2.cloudflarestorage.com",
        AuthenticationRegion = "auto",
        ForcePathStyle = true,
        RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
        ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
    }));
builder.Services.AddMedia(builder.Configuration);

builder.Services.AddSales(builder.Configuration);
builder.Services.AddShipping();
builder.Services.AddAppointments();
builder.Services.AddCompany();
builder.Services.AddContact();
builder.Services.AddBlogs();
builder.Services.AddFaqs();
builder.Services.AddGeo(builder.Configuration);
builder.Services.AddOpenStreetMap(builder.Configuration);
builder.Services.AddGitHub(builder.Configuration);
builder.Services.AddInbox();
builder.Services.AddModeration();
builder.Services.AddNotifications(builder.Configuration);
builder.Services.AddMessages(builder.Configuration);
builder.Services.AddWidget(builder.Configuration);

// In-process only: any service may inject IRssService to read a feed, and no route exposes it.
builder.Services.AddRss(builder.Configuration);

// Feature services never reference each other; the host plugs their ports together here.
builder.Services.AddIntegrations();

// Last: it discovers MCP-marked controller actions from every assembly registered above.
builder.Services.AddMcp(builder.Configuration);

// One generated document per controller-owning service registered above.
ServiceOpenApiDocuments.AddTo(builder.Services, mvc.PartManager);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi("/openapi/{documentName}.json");
}

app.UseHttpsRedirection();

app.UseCors(MarketingCors.PolicyName);

// Order matters: authentication establishes who the caller is, authorization then decides.
app.UseAuthentication();
// Keep rate limiting after authentication so report and message policies partition by authenticated sub.
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapMessages();
app.MapMcpEndpoint();
app.MapHealthChecks("/health");

app.Run();

static string Required(IConfigurationSection section, string key) =>
    section[key] is { } value && !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new InvalidOperationException($"{section.Path}:{key} is required.");

// Exposes the entry point to the black-box API integration tests.
public partial class Program;
