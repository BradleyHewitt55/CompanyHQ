using System.Text.Json.Serialization;
using Api.Cors;
using Api.Data;
using Api.Integrations;
using Api.OpenApi;
using Amazon.Runtime;
using Amazon.S3;
using Api.Validation;
using CompanyHQ.Appointments.Extensions;
using CompanyHQ.Auth.Extensions;
using CompanyHQ.Blogs.Extensions;
using CompanyHQ.Cache.Contracts.Models;
using CompanyHQ.Cache.Extensions;
using CompanyHQ.Company.Extensions;
using CompanyHQ.Contact.Extensions;
using CompanyHQ.Email.Extensions;
using CompanyHQ.Faqs.Extensions;
using CompanyHQ.Geo.Extensions;
using CompanyHQ.GitHub.Extensions;
using CompanyHQ.Inbox.Extensions;
using CompanyHQ.Mcp.Extensions;
using CompanyHQ.Media.Extensions;
using CompanyHQ.Messages.Extensions;
using CompanyHQ.Moderation.Extensions;
using CompanyHQ.Notifications.Extensions;
using CompanyHQ.OpenStreetMap.Extensions;
using CompanyHQ.Postgres.Extensions;
using CompanyHQ.Rss.Extensions;
using CompanyHQ.Users.Extensions;
using CompanyHQ.Widget.Extensions;
using CompanyHQ.Sales.Extensions;
using CompanyHQ.Shipping.Extensions;
using CompanyHQ.ServiceCatalog;
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

// Optional transports opt into boot without credentials. Configured transports keep SDK validation.
if ((builder.Configuration["ServiceProviders:Email"] ?? "resend") == "resend")
    builder.Services.AddEmail(builder.Configuration, allowUnconfigured: true);
else
    builder.Services.AddUnconfiguredEmail(builder.Configuration);

var r2 = builder.Configuration.GetSection("Cloudflare:R2");
var s3Selected = (builder.Configuration["ServiceProviders:Media"] ?? "s3") == "s3";
if (s3Selected && new[] { "AccountId", "AccessKeyId", "SecretAccessKey" }.All(key => !string.IsNullOrWhiteSpace(r2[key]))
    && !string.IsNullOrWhiteSpace(builder.Configuration["Media:Bucket"]))
{
    builder.Services.AddSingleton<IAmazonS3>(new AmazonS3Client(
        new BasicAWSCredentials(r2["AccessKeyId"]!, r2["SecretAccessKey"]!),
        new AmazonS3Config
        {
            ServiceURL = $"https://{r2["AccountId"]}.r2.cloudflarestorage.com",
            AuthenticationRegion = "auto", ForcePathStyle = true,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        }));
    builder.Services.AddMedia(builder.Configuration);
}
else builder.Services.AddUnconfiguredMedia(builder.Configuration);

builder.Services.AddSales(builder.Configuration);
builder.Services.AddShipping();
builder.Services.AddAppointments();
builder.Services.AddCompany();
builder.Services.AddCompanyServiceManagement();
builder.Services.AddContact();
builder.Services.AddBlogs();
builder.Services.AddFaqs();
if (!string.IsNullOrWhiteSpace(builder.Configuration["Geo:UserAgent"]))
{
    builder.Services.AddGeo(builder.Configuration);
    builder.Services.AddOpenStreetMap(builder.Configuration);
}
else builder.Services.AddGeo(builder.Configuration, allowUnconfigured: true);
builder.Services.AddGitHub(builder.Configuration);
builder.Services.AddInbox();
builder.Services.AddModeration();
builder.Services.AddNotifications(builder.Configuration, allowUnconfigured: true,
    useFirebase: builder.Configuration.GetValue("ServiceCatalog:LoadFirebase", true));
builder.Services.AddMessages(builder.Configuration);
builder.Services.AddWidget(builder.Configuration);

// In-process only: any service may inject IRssService to read a feed, and no route exposes it.
builder.Services.AddRss(builder.Configuration);

// Feature services never reference each other; the host plugs their ports together here.
builder.Services.AddIntegrations();

builder.Services.AddHqServiceDescriptors(builder.Configuration);

// Last: it discovers MCP-marked controller actions from every assembly registered above.
builder.Services.AddMcp(builder.Configuration);
builder.Services.AddServiceCatalog(HqServiceCatalog.AllowedServices);

// One generated document per controller-owning service registered above.
ServiceOpenApiDocuments.AddTo(builder.Services, mvc.PartManager);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi("/openapi/{documentName}.json");
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseServiceCatalog();

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

// Exposes the entry point to the black-box API integration tests.
public partial class Program;
