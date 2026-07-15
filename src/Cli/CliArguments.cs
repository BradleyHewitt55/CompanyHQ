namespace Cli;

public static class CliArguments
{
    private static readonly IReadOnlyDictionary<string, string> LegacyAliases = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["-db"] = "--database",
        ["-sip"] = "--server-ip",
        ["-conf"] = "--config"
    };

    public static string[] Normalize(IEnumerable<string> args) =>
        args.Select(arg => LegacyAliases.GetValueOrDefault(arg, arg)).ToArray();
}
