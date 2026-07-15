using System.ComponentModel;
using Spectre.Console.Cli;
using Spectre.Console;

namespace Cli;

public sealed class NewCompanySettings : CommandSettings
{
    [CommandOption("-n|--name <NAME>")][Description("Company name.")] public string Name { get; init; } = string.Empty;
    [CommandOption("--database <PROVIDER>")] public string Database { get; init; } = "postgres";
    [CommandOption("--server-ip <IP>")] public string? ServerIp { get; init; }
    [CommandOption("-u|--username <USERNAME>")] public string? Username { get; init; }
    [CommandOption("--config <DIRECTORY>")] public string? ConfigDirectory { get; init; }
    [CommandOption("--output <DIRECTORY>")] public string Output { get; init; } = Path.Combine("CompanyHQ", "Companies");
    [CommandOption("--no-start")] public bool NoStart { get; init; }
    public override ValidationResult Validate() => string.IsNullOrWhiteSpace(Name) ? ValidationResult.Error("Company name is required (-n).") : ValidationResult.Success();
}
