namespace CompanyHQ.Services.Mcp.Contracts.Models;

/// <summary>Public OAuth endpoints and lifetimes for the CompanyHQ MCP resource.</summary>
public sealed class McpOAuthOptions
{
    public const string SectionName = "Mcp:OAuth";

    public string PublicWebBaseAddress { get; set; } = string.Empty;

    public string AuthorizationServerBaseAddress { get; set; } = "http://localhost:4002";

    public string ResourceAddress { get; set; } = "http://localhost:4002/mcp";

    public string? ResourceMetadataAddress { get; set; }

    /// <summary>
    /// Exact HTTPS redirect URIs registered for confidential clients. Loopback HTTP callbacks are
    /// accepted with a dynamic port for native clients.
    /// </summary>
    public string[] AllowedRedirectUris { get; set; } = [];

    public int AuthorizationCodeMinutes { get; set; } = 5;

    /// <summary>
    /// Long-lived but revocable, matching legacy agent-key behavior. Refresh-capable clients also
    /// receive a rotating refresh token so an agent can renew access without another login.
    /// </summary>
    public int AccessTokenMinutes { get; set; } = 43_200;

    public int RefreshTokenDays { get; set; } = 30;

    public Uri AuthorizationEndpoint => new(AuthorizationServerBaseAddress.TrimEnd('/') + "/oauth/authorize");

    public Uri TokenEndpoint => new(AuthorizationServerBaseAddress.TrimEnd('/') + "/oauth/token");

    public Uri RegistrationEndpoint => new(AuthorizationServerBaseAddress.TrimEnd('/') + "/oauth/register");

    public Uri ProtectedResourceMetadataEndpoint => new(
        ResourceMetadataAddress?.TrimEnd('/')
        ?? AuthorizationServerBaseAddress.TrimEnd('/') + "/.well-known/oauth-protected-resource");

    public TimeSpan AuthorizationCodeLifetime => TimeSpan.FromMinutes(AuthorizationCodeMinutes);

    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(AccessTokenMinutes);

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(RefreshTokenDays);

    public void Validate()
    {
        ValidateAbsolute(PublicWebBaseAddress, nameof(PublicWebBaseAddress));
        ValidateAbsolute(AuthorizationServerBaseAddress, nameof(AuthorizationServerBaseAddress));
        ValidateAbsolute(ResourceAddress, nameof(ResourceAddress));
        if (ResourceMetadataAddress is not null)
        {
            ValidateAbsolute(ResourceMetadataAddress, nameof(ResourceMetadataAddress));
        }

        if (AuthorizationCodeMinutes <= 0 || AccessTokenMinutes <= 0 || RefreshTokenDays <= 0)
        {
            throw new InvalidOperationException("MCP OAuth token lifetimes must be positive.");
        }

        foreach (var redirectUri in AllowedRedirectUris)
        {
            if (!IsValidRedirectUri(redirectUri))
            {
                throw new InvalidOperationException($"Invalid configured MCP OAuth redirect URI: {redirectUri}");
            }
        }
    }

    public bool IsRedirectUriAllowed(string redirectUri)
    {
        if (!IsValidRedirectUri(redirectUri))
        {
            return false;
        }

        var configured = AllowedRedirectUris;
        if (configured.Length > 0)
        {
            return configured.Any(candidate => UriEquals(candidate, redirectUri)
                || (IsLoopback(candidate) && IsLoopback(redirectUri) && RedirectUrisMatch(candidate, redirectUri)));
        }

        return IsLoopback(redirectUri) || new Uri(redirectUri).Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
    }

    public static bool RedirectUrisMatch(string expected, string presented)
    {
        if (UriEquals(expected, presented))
        {
            return true;
        }

        if (!IsLoopback(expected) || !IsLoopback(presented))
        {
            return false;
        }

        var expectedUri = new Uri(expected);
        var presentedUri = new Uri(presented);
        return string.Equals(expectedUri.Scheme, presentedUri.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(expectedUri.Host, presentedUri.Host, StringComparison.OrdinalIgnoreCase)
            && string.Equals(expectedUri.AbsolutePath, presentedUri.AbsolutePath, StringComparison.Ordinal)
            && string.Equals(expectedUri.Query, presentedUri.Query, StringComparison.Ordinal);
    }

    public static bool IsValidRedirectUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || !string.IsNullOrEmpty(uri.Fragment)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        if (uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrEmpty(uri.Host);
        }

        return uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) && IsLoopback(uri);
    }

    public static bool IsLoopback(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && IsLoopback(uri);

    private static bool IsLoopback(Uri uri) =>
        uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
        && (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("[::1]", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("::1", StringComparison.OrdinalIgnoreCase));

    private static bool UriEquals(string left, string right) =>
        Uri.TryCreate(left, UriKind.Absolute, out var leftUri)
        && Uri.TryCreate(right, UriKind.Absolute, out var rightUri)
        && string.Equals(leftUri.GetComponents(UriComponents.HttpRequestUrl, UriFormat.UriEscaped),
            rightUri.GetComponents(UriComponents.HttpRequestUrl, UriFormat.UriEscaped),
            StringComparison.Ordinal);

    private static void ValidateAbsolute(string value, string name)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || !string.IsNullOrEmpty(uri.Fragment)
            || (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) && !IsLoopback(uri)))
        {
            throw new InvalidOperationException($"MCP OAuth {name} must be an HTTPS URL, or a localhost development URL, without a fragment.");
        }
    }
}
