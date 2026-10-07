using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Services.Postgres.Contracts.Forms;

/// <summary>
/// Keyword filter for a list endpoint. Bindable straight from the query string
/// (<c>?field=title&amp;keyword=launch</c>). <see cref="Field"/> is any field of the shape the
/// caller's role receives; leave it out to match the keyword against every text field of that shape.
/// </summary>
public sealed record SearchRequest
{
    [MaxLength(64)]
    public string? Field { get; init; }

    [MaxLength(200)]
    public string? Keyword { get; init; }

    public static SearchRequest For(string? keyword, string? field = null) =>
        new() { Field = field, Keyword = keyword };
}
