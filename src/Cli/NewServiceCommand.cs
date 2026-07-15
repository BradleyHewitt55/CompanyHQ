using System.ComponentModel;
using Services.Scaffolding;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Cli;

public sealed class NewServiceSettings : CommandSettings
{
    [CommandOption("-n|--name <NAME>")]
    [Description("PascalCase service name.")]
    public string Name { get; init; } = string.Empty;

    [CommandOption("-p|--parent <SERVICE>")]
    [Description("Owning parent service.")]
    public string Parent { get; init; } = string.Empty;

    [CommandOption("-o|--output <DIRECTORY>")]
    public string Output { get; init; } = Directory.GetCurrentDirectory();

    public override ValidationResult Validate() => ServiceScaffolder.IsValidName(Name) && !string.IsNullOrWhiteSpace(Parent)
        ? ValidationResult.Success()
        : ValidationResult.Error("Service name must be PascalCase and an owning --parent is required.");
}

public sealed class NewServiceCommand : Command<NewServiceSettings>
{
    protected override int Execute(CommandContext context, NewServiceSettings settings, CancellationToken cancellationToken)
    {
        var directory = ServiceScaffolder.Generate(settings.Output, settings.Name, settings.Parent);
        AnsiConsole.MarkupLine($"Created [green]{settings.Name}[/] at {directory}");
        return 0;
    }
}
