namespace CompanyHQ.Cli;

public enum CliCommand
{
    CreateCompany,
    ScaffoldService,
}

public sealed record CliArguments(
    CliCommand Command,
    string? CompanyName,
    string? ServiceName,
    string? SshUsername,
    string? ServerIp,
    string? Password,
    string? KeyDirectory,
    string? ServerRoot);

public sealed record CliParseResult(CliArguments? Arguments, string? Error)
{
    public bool IsSuccess => Arguments is not null;
}

public static class CliArgumentParser
{
    public const string ServiceVerb = "service";

    public const string Usage =
        "Usage: companyhq --c <CompanyName> [--root <path>] "
        + "[--sip <ip> --u <username> [--p <password> | --conf <key-directory>]]\n"
        + "       companyhq service <ServiceName> [--c <CompanyName>] [--root <path>]\n"
        + "       Run the service verb inside a company directory to omit --c and --root.";

    public static CliParseResult Parse(IReadOnlyList<string> args)
    {
        var command = CliCommand.CreateCompany;
        string? serviceName = null;
        var index = 0;

        if (args.Count > 0 && args[0].Equals(ServiceVerb, StringComparison.OrdinalIgnoreCase))
        {
            command = CliCommand.ScaffoldService;
            if (args.Count < 2 || args[1].StartsWith("--", StringComparison.Ordinal))
            {
                return new(null, "Missing required '<ServiceName>' after 'service'.");
            }

            serviceName = args[1];
            index = 2;
        }

        string? company = null;
        string? username = null;
        string? serverIp = null;
        string? password = null;
        string? keyDirectory = null;
        string? serverRoot = null;

        for (; index < args.Count; index++)
        {
            var flag = args[index];
            if (flag is not ("--c" or "--u" or "--sip" or "--p" or "--conf" or "--root"))
            {
                return new(null, $"Unknown argument '{flag}'.");
            }

            if (++index >= args.Count)
            {
                return new(null, $"Missing value for '{flag}'.");
            }

            switch (flag)
            {
                case "--c": company = args[index]; break;
                case "--u": username = args[index]; break;
                case "--sip": serverIp = args[index]; break;
                case "--p": password = args[index]; break;
                case "--conf": keyDirectory = args[index]; break;
                case "--root": serverRoot = args[index]; break;
            }
        }

        if (command == CliCommand.ScaffoldService)
        {
            if (serverIp is not null || username is not null)
            {
                return new(null, "The 'service' verb does not support remote options.");
            }

            // Company stays optional here: Program resolves it from the working
            // directory when omitted, which is the whole point of the verb.
            return new(new CliArguments(
                command, company, serviceName, null, null, null, null, serverRoot), null);
        }

        if (string.IsNullOrWhiteSpace(company))
        {
            return new(null, "Missing required '--c <CompanyName>'.");
        }

        if (serverIp is not null && string.IsNullOrWhiteSpace(username))
        {
            return new(null, "Remote mode requires '--u <username>'.");
        }

        return new(new CliArguments(
            command, company, null, username, serverIp, password, keyDirectory, serverRoot), null);
    }
}
