using System.ComponentModel;

using ModelContextProtocol.Server;

using CompanyHQ.Geo.Contracts.Dtos;
using CompanyHQ.Geo.Services;

namespace CompanyHQ.Geo.Mcp;

[McpServerToolType]
public sealed class GeoTools(IGeoService geo)
{
    [McpServerTool(Name = "search_area")]
    public async Task<IReadOnlyList<AreaDto>> SearchArea(
        [Description("The area name or partial address to search for.")] string query,
        [Description("Maximum results, capped at 20. Defaults to 10.")] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var capped = Math.Clamp(limit <= 0 ? 10 : limit, 1, 20);
        return await geo.SearchAreasAsync(query, capped, cancellationToken);
    }
}
