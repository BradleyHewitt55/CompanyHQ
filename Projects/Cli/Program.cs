using Abstractions.Companies;
using CompanyHQ.Cli;
using CompanyHQ.Infrastructure.PluginDiscovery;
using CompanyHQ.Services.Companies.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var parsed = CliArgumentParser.Parse(args);
if (!parsed.IsSuccess)
{
    Console.Error.WriteLine(parsed.Error);
    Console.Error.WriteLine(CliArgumentParser.Usage);
    return 1;
}

var arguments = parsed.Arguments!;
var console = new SystemCompanyConsole();

// Resolved before configuration is built so that being inside a company directory
// supplies the server root exactly as an explicit --root would.
var location = arguments.Command == CliCommand.ScaffoldService
    ? new CompanyLocator().Locate(Directory.GetCurrentDirectory())
    : null;

var companyName = arguments.CompanyName ?? location?.CompanyName;
var serverRoot = arguments.ServerRoot ?? location?.ServerRoot;

if (arguments.Command == CliCommand.ScaffoldService && companyName is null)
{
    Console.Error.WriteLine(
        "Not inside a company directory. Run this from <ServerRoot>/Companies/<Company> "
        + "or pass '--c <CompanyName>'.");
    return 1;
}

if (arguments.ServerIp is not null)
{
    var password = arguments.Password;
    if (arguments.KeyDirectory is null && password is null)
    {
        await console.WriteAsync("SSH password: ");
        password = await console.ReadSecretAsync();
    }

    try
    {
        var selection = SshConnectionSelector.Select(arguments with { Password = password });
        await new SshConnectionFactory().Create(selection)
            .RunAsync(companyName!, CancellationToken.None);
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

var settings = new Dictionary<string, string?>
{
    ["CompanyHq:Company"] = companyName,
};
if (serverRoot is not null)
{
    settings["CompanyHq:ServerRoot"] = serverRoot;
}

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .AddInMemoryCollection(settings)
    .Build();
var services = new ServiceCollection();
services.AddLogging();
services.AddSingleton<IConfiguration>(config);
services.AddPluginDiscovery(config);

await using var provider = services.BuildServiceProvider();
try
{
    if (arguments.Command == CliCommand.ScaffoldService)
    {
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICompanyContext>().Current = companyName;
        await scope.ServiceProvider.GetRequiredService<IServiceScaffolder>()
            .ScaffoldAsync(arguments.ServiceName!);

        var created = Path.Combine(
            provider.GetRequiredService<IServerPaths>().CompanyDirectory(companyName!),
            "Services",
            arguments.ServiceName!);
        await console.WriteLineAsync($"Created {created}");
        return 0;
    }

    await provider.GetRequiredService<CompanyCreation>().CreateAsync(companyName!, console);
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

public partial class Program;
