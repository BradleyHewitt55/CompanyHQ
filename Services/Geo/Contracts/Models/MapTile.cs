using System.Globalization;

namespace CompanyHQ.Geo.Contracts.Models;

/// <summary>One cell of the fixed slippy-map grid the nearby-place cache is keyed on.</summary>
/// <remarks>
/// Zoom 12 gives a tile about 9.8 km across at the equator and about 8.8 km at mid latitudes —
/// close to what one 15 km scan used to cover, so a cold tile costs roughly what the old query did
/// while a warm one is reusable by every viewport that touches it. Keying the cache on the caller's
/// centre point instead meant a 110 m pan missed, and a 5 km result could never satisfy a 15 km
/// request: nested radii do not compose, an absolute grid does.
/// </remarks>
public readonly record struct MapTile(int Zoom, int X, int Y)
{
    public const int GridZoom = 12;

    private const int GridSize = 1 << GridZoom;

    /// <summary>Web Mercator cannot express the poles; this is the standard cut-off.</summary>
    private const double MercatorLimit = 85.05112878;

    /// <summary>The tile a coordinate falls in. Tiles are disjoint, so this assignment is total.</summary>
    public static MapTile From(double latitude, double longitude)
    {
        var latitudeRadians = Math.Clamp(latitude, -MercatorLimit, MercatorLimit) * Math.PI / 180.0;
        var x = (int)Math.Floor((Math.Clamp(longitude, -180, 180) + 180.0) / 360.0 * GridSize);
        var y = (int)Math.Floor((1.0 - Math.Asinh(Math.Tan(latitudeRadians)) / Math.PI) / 2.0 * GridSize);

        return new MapTile(GridZoom, Math.Clamp(x, 0, GridSize - 1), Math.Clamp(y, 0, GridSize - 1));
    }

    /// <summary>The identifier a client uses to remember it already holds this tile.</summary>
    public string Id => string.Create(CultureInfo.InvariantCulture, $"{Zoom}/{X}/{Y}");

    public GeoViewport Bounds => new(LatitudeOf(Y + 1), LongitudeOf(X), LatitudeOf(Y), LongitudeOf(X + 1));

    /// <summary>Every tile touching a viewport.</summary>
    public static IReadOnlyList<MapTile> Cover(GeoViewport viewport)
    {
        var (xMin, yMin, xMax, yMax) = Range(viewport);
        var tiles = new List<MapTile>((xMax - xMin + 1) * (yMax - yMin + 1));

        for (var x = xMin; x <= xMax; x++)
        {
            for (var y = yMin; y <= yMax; y++)
            {
                tiles.Add(new MapTile(GridZoom, x, y));
            }
        }

        return tiles;
    }

    /// <summary>Counts <see cref="Cover"/> without allocating it, for the cap check.</summary>
    public static long CountCovering(GeoViewport viewport)
    {
        var (xMin, yMin, xMax, yMax) = Range(viewport);

        return (long)(xMax - xMin + 1) * (yMax - yMin + 1);
    }

    /// <summary>
    /// The tile-aligned rectangle containing every given tile, plus every tile inside it. One
    /// provider query covers the whole rectangle, so the tiles that were merely in the way get
    /// cached from it too rather than being thrown away.
    /// </summary>
    public static (GeoViewport Bounds, IReadOnlyList<MapTile> Tiles) Enclose(IReadOnlyCollection<MapTile> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);

        var xMin = tiles.Min(tile => tile.X);
        var xMax = tiles.Max(tile => tile.X);
        var yMin = tiles.Min(tile => tile.Y);
        var yMax = tiles.Max(tile => tile.Y);

        var bounds = new GeoViewport(
            LatitudeOf(yMax + 1),
            LongitudeOf(xMin),
            LatitudeOf(yMin),
            LongitudeOf(xMax + 1));

        return (bounds, Cover(bounds));
    }

    private static (int XMin, int YMin, int XMax, int YMax) Range(GeoViewport viewport)
    {
        var topLeft = From(viewport.North, viewport.West);
        var bottomRight = From(viewport.South, viewport.East);

        return (
            Math.Min(topLeft.X, bottomRight.X),
            Math.Min(topLeft.Y, bottomRight.Y),
            Math.Max(topLeft.X, bottomRight.X),
            Math.Max(topLeft.Y, bottomRight.Y));
    }

    private static double LatitudeOf(int y) =>
        Math.Atan(Math.Sinh(Math.PI * (1.0 - 2.0 * Math.Clamp(y, 0, GridSize) / (double)GridSize))) * 180.0 / Math.PI;

    private static double LongitudeOf(int x) => Math.Clamp(x, 0, GridSize) / (double)GridSize * 360.0 - 180.0;
}
