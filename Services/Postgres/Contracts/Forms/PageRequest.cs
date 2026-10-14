namespace CompanyHQ.Postgres.Contracts.Forms;

/// <summary>
/// Paging window for a list endpoint. Bindable straight from the query string
/// (<c>?page=2&amp;pageSize=10</c>).
/// </summary>
public sealed record PageRequest
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 10;

    private readonly int page = 1;
    private readonly int pageSize = DefaultPageSize;

    /// <summary>One-based. Clamped rather than rejected — a bad page number is not worth a 400.</summary>
    public int Page
    {
        get => page;
        init => page = Math.Max(value, 1);
    }

    /// <summary>Capped at <see cref="MaxPageSize"/> so no caller can ask for the whole table.</summary>
    public int PageSize
    {
        get => pageSize;
        init => pageSize = Math.Clamp(value, 1, MaxPageSize);
    }

    /// <summary>Bounded for the 32-bit offset accepted by LINQ and PostgreSQL providers.</summary>
    public int Skip => (int)Math.Min((long)(Page - 1) * PageSize, int.MaxValue);
}
