namespace CompanyHQ.Geo.Contracts.Models;

/// <summary>A south/west/north/east bounding box in degrees — the area a map request asks about.</summary>
public readonly record struct GeoViewport(double South, double West, double North, double East)
{
    /// <summary>
    /// The cost cap that replaces the old server-owned 15 km radius. The caller now chooses the
    /// area, so the ceiling has to be enforced here or a client could ask a public provider for a
    /// continent. 24 is set just above what the old fixed radius needed — a 15 km box covers 20
    /// tiles at <see cref="MapTile.GridZoom"/> — so nothing that used to work is now refused, while
    /// a fully cold fetch stays around 35 km × 53 km, the same order of work as the scan it
    /// replaces. The map opens on a 5 km radius, which is 4 tiles.
    /// </summary>
    public const int MaxTiles = 24;

    /// <summary>
    /// Three decimals, same as every other coordinate this service touches. The box is rounded
    /// <b>outward</b> so it can never collapse to zero height on a very small viewport, and so a
    /// caller never gets less area than they asked for.
    /// </summary>
    public GeoViewport Rounded() => new(
        Math.Clamp(Math.Floor(South * 1000) / 1000, -90, 90),
        Math.Clamp(Math.Floor(West * 1000) / 1000, -180, 180),
        Math.Clamp(Math.Ceiling(North * 1000) / 1000, -90, 90),
        Math.Clamp(Math.Ceiling(East * 1000) / 1000, -180, 180));

    /// <summary>
    /// Validates a caller-supplied box and applies the tile cap. Returns the rounded viewport the
    /// service should actually use, so the controller never hands on the raw numbers.
    /// </summary>
    public static bool TryCreate(
        double south,
        double west,
        double north,
        double east,
        out GeoViewport viewport,
        out string? error)
    {
        viewport = default;
        error = null;

        if (!IsLatitude(south) || !IsLatitude(north))
        {
            error = "south and north must be between -90 and 90.";

            return false;
        }

        if (!IsLongitude(west) || !IsLongitude(east))
        {
            error = "west and east must be between -180 and 180.";

            return false;
        }

        if (south >= north)
        {
            error = "south must be less than north.";

            return false;
        }

        // A box wrapping the antimeridian is two rectangles on the tile grid. Nothing asks for one,
        // so it is refused rather than silently halved into the wrong half.
        if (west >= east)
        {
            error = "west must be less than east; a viewport crossing the antimeridian is not supported.";

            return false;
        }

        var candidate = new GeoViewport(south, west, north, east).Rounded();
        var tiles = MapTile.CountCovering(candidate);

        if (tiles > MaxTiles)
        {
            error = $"The viewport covers {tiles} map tiles; the maximum is {MaxTiles}. Zoom in and try again.";

            return false;
        }

        viewport = candidate;

        return true;
    }

    private static bool IsLatitude(double value) => !double.IsNaN(value) && value is >= -90 and <= 90;

    private static bool IsLongitude(double value) => !double.IsNaN(value) && value is >= -180 and <= 180;
}
