using CompanyHQ.Services.Widget.Contracts.Enums;

namespace CompanyHQ.Services.Widget.Contracts.Ports;

/// <summary>A report to file, with the source already checked against the allowed list.</summary>
public sealed record WidgetIssue(
    string Category,
    string Title,
    string Message,
    string? Email,
    string Source,
    string? Origin);

/// <summary>
/// Where widget bug reports are filed. Optional: with no bridge the widget reports issues as
/// unavailable.
/// </summary>
public interface IWidgetIssueTracker
{
    /// <summary>False while the tracker has nowhere to file, so a UI can hide the form.</summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    Task<WidgetError> SubmitAsync(WidgetIssue issue, CancellationToken cancellationToken = default);
}
