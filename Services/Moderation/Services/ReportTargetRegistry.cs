using CompanyHQ.Moderation.Contracts.Ports;
using CompanyHQ.Moderation.Contracts.Dtos;
using CompanyHQ.Moderation.Contracts.Enums;

namespace CompanyHQ.Moderation.Services;

/// <inheritdoc cref="IReportTargetRegistry" />
public sealed class ReportTargetRegistry : IReportTargetRegistry
{
    private readonly IReadOnlyDictionary<string, ReportTargetDefinition> definitions;

    public ReportTargetRegistry(IEnumerable<IReportableContentSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var registered = new Dictionary<string, ReportTargetDefinition>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            ArgumentNullException.ThrowIfNull(source);

            if (!ReportTargetKind.TryNormalize(source.TargetKind, out var targetKind))
            {
                throw new InvalidOperationException(
                    $"Report source '{source.GetType().Name}' has an invalid target kind.");
            }

            var reasons = source.AllowedReasons?.ToArray() ?? [];
            if (reasons.Length == 0
                || reasons.Any(reason => !Enum.IsDefined(reason))
                || reasons.Distinct().Count() != reasons.Length)
            {
                throw new InvalidOperationException(
                    $"Report source '{source.GetType().Name}' must declare a non-empty, duplicate-free reason catalog.");
            }

            if (!registered.TryAdd(
                    targetKind,
                    new ReportTargetDefinition(targetKind, source, reasons)))
            {
                throw new InvalidOperationException(
                    $"Duplicate report source registration for target kind '{targetKind}'.");
            }
        }

        definitions = registered;
    }

    public bool TryGet(string? targetKind, out ReportTargetDefinition definition)
    {
        if (ReportTargetKind.TryNormalize(targetKind, out var normalized)
            && definitions.TryGetValue(normalized, out var found))
        {
            definition = found;
            return true;
        }

        definition = default!;
        return false;
    }

    public ReportOptionsDto? GetOptions(string? targetKind) =>
        TryGet(targetKind, out var definition)
            ? new ReportOptionsDto(
                definition.TargetKind,
                definition.AllowedReasons
                    .Select(reason => new ReportReasonOptionDto(
                        reason,
                        reason is ReportReason.Other))
                    .ToArray())
            : null;
}

/// <summary>Shared target-kind normalization rules for report and hide operations.</summary>
public static class ReportTargetKind
{
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized.Length > 0
            && normalized.Length <= 32
            && normalized.All(character => char.IsAsciiLetterOrDigit(character)
                || character is '-' or '_');
    }

    public static string Normalize(string value)
    {
        if (!TryNormalize(value, out var normalized))
        {
            throw new ArgumentException("The target kind is invalid.", nameof(value));
        }

        return normalized;
    }
}
