using Microsoft.Extensions.Configuration;

namespace Api.Cors;

/// <summary>
/// The browser origins allowed to call this API from the static marketing site (<c>Apps/Marketing</c>).
/// </summary>
/// <remarks>
/// An explicit list rather than a wildcard: the marketing site is the only cross-origin browser
/// caller, and a wildcard here would also open every anonymous read endpoint to any page on the web.
/// </remarks>
public static class MarketingCors
{
    public const string PolicyName = "marketing-site";

    /// <summary>
    /// <c>Marketing:Origins</c>. From the environment that is <c>Marketing__Origins__0</c>,
    /// <c>Marketing__Origins__1</c>, …
    /// </summary>
    public static string[] Origins(IConfiguration configuration) =>
        configuration.GetSection("Marketing:Origins").Get<string[]>() ?? [];
}
