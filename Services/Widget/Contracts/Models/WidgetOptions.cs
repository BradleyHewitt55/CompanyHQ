namespace CompanyHQ.Services.Widget.Contracts.Models;

/// <summary>Which sites and apps may send through the widget.</summary>
public sealed class WidgetOptions
{
    public const string SectionName = "Widget";

    /// <summary>
    /// The source names a submission may carry, such as <c>Website</c> or <c>App</c>. The first is
    /// used when a submission names none; one outside the list is refused.
    /// </summary>
    public string[] Sources { get; set; } = ["Website"];

    public void Validate()
    {
        if (Sources.Length == 0 || Sources.Any(source => string.IsNullOrWhiteSpace(source) || source.Trim().Length > 40))
        {
            throw new InvalidOperationException("Widget:Sources must name at least one source of 1 to 40 characters.");
        }
    }
}
