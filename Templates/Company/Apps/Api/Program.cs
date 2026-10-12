using System.Text.Json.Serialization;
using CompanyHQ.Services.Rss.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Every service's constructor dependencies are checked when the host builds, so a registration
// with a missing dependency stops startup instead of failing the first request that needs it.
builder.Host.UseDefaultServiceProvider((context, provider) =>
{
    provider.ValidateOnBuild = true;
    provider.ValidateScopes = context.HostingEnvironment.IsDevelopment();
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false)));
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

// In-process only: your controllers and services inject IRssService to read a feed. It maps no
// route, so this API exposes nothing about RSS until a feature of yours chooses to.
builder.Services.AddRss(builder.Configuration);

var app = builder.Build();

app.MapControllers();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapHealthChecks("/health");

app.Run();

// Exposes the entry point to integration tests you may add.
public partial class Program;
