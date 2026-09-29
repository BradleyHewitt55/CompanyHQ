using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

using CompanyHQ.Services.Mcp.Contracts.Dtos;
using CompanyHQ.Services.Mcp.Contracts.Forms;
using CompanyHQ.Services.Mcp.Contracts.Models;
using CompanyHQ.Services.Mcp.Services;
using CompanyHQ.Services.Auth.Contracts.Identity;

namespace CompanyHQ.Services.Mcp.Controllers;

/// <summary>OAuth 2.1 endpoints for the protected CompanyHQ MCP resource.</summary>
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("oauth")]
public sealed class McpOAuthController(
    IMcpOAuthService oauth,
    McpOAuthOptions options) : ControllerBase
{
    [HttpGet("authorize")]
    [AllowAnonymous]
    public async Task<IActionResult> Authorize(
        [FromQuery(Name = "response_type")] string? responseType,
        [FromQuery(Name = "client_id")] string? clientId,
        [FromQuery(Name = "redirect_uri")] string? redirectUri,
        [FromQuery(Name = "code_challenge")] string? codeChallenge,
        [FromQuery(Name = "code_challenge_method")] string? codeChallengeMethod,
        [FromQuery] string? resource,
        [FromQuery] string? scope,
        [FromQuery] string? state,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(responseType)
            || string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(redirectUri)
            || string.IsNullOrWhiteSpace(codeChallenge)
            || string.IsNullOrWhiteSpace(codeChallengeMethod)
            || string.IsNullOrWhiteSpace(resource))
        {
            return OAuthError("invalid_request");
        }

        var request = new McpAuthorizeRequest(
            responseType,
            clientId,
            redirectUri,
            codeChallenge,
            codeChallengeMethod,
            resource,
            scope,
            state);
        var validation = Validate(request);
        if (validation is not null)
        {
            return OAuthError(validation);
        }

        if (User.GetUserId() is not { } userId)
        {
            // The API does not own the browser session. The web app turns its existing session into
            // an Authorization header when the user approves this exact request.
            var webAuthorize = new Uri(options.PublicWebBaseAddress.TrimEnd('/') + "/oauth/authorize");
            var query = Request.Query.ToDictionary(pair => pair.Key, pair => (StringValues)pair.Value);
            return Redirect(QueryHelpers.AddQueryString(webAuthorize.ToString(), query));
        }

        var code = await oauth.IssueAuthorizationCodeAsync(userId, request, cancellationToken);
        if (code is null)
        {
            return OAuthError("invalid_request");
        }
        var redirect = QueryHelpers.AddQueryString(request.RedirectUri, "code", code);
        if (!string.IsNullOrEmpty(request.State))
        {
            redirect = QueryHelpers.AddQueryString(redirect, "state", request.State);
        }

        return Redirect(redirect);
    }

    [HttpPost("token")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Token(
        [FromForm] string? grant_type,
        [FromForm] string? client_id,
        [FromForm] string? code,
        [FromForm] string? redirect_uri,
        [FromForm] string? code_verifier,
        [FromForm] string? refresh_token,
        [FromForm] string? resource,
        CancellationToken cancellationToken = default)
    {
        DisableCaching();

        if (string.Equals(grant_type, "refresh_token", StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(client_id)
                || string.IsNullOrWhiteSpace(refresh_token)
                || string.IsNullOrWhiteSpace(resource))
            {
                return OAuthError("invalid_request");
            }

            var refreshResult = await oauth.RedeemRefreshTokenAsync(
                new McpRefreshTokenRequest(grant_type!, client_id, refresh_token, resource),
                cancellationToken);

            return refreshResult.Succeeded ? Ok(refreshResult.Token) : OAuthError(refreshResult.Error!);
        }

        if (!string.Equals(grant_type, "authorization_code", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(client_id)
            || string.IsNullOrWhiteSpace(code)
            || string.IsNullOrWhiteSpace(redirect_uri)
            || string.IsNullOrWhiteSpace(code_verifier)
            || string.IsNullOrWhiteSpace(resource))
        {
            return OAuthError("invalid_request");
        }

        var result = await oauth.RedeemAuthorizationCodeAsync(
            new McpTokenRequest(grant_type!, client_id, code, redirect_uri, code_verifier, resource),
            cancellationToken);

        return result.Succeeded ? Ok(result.Token) : OAuthError(result.Error!);
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(McpOAuthRateLimits.ClientRegistration)]
    [Consumes("application/json")]
    public async Task<ActionResult<McpOAuthClientDto>> Register(
        RegisterMcpClientRequest request,
        CancellationToken cancellationToken = default)
    {
        DisableCaching();

        if (request.RedirectUris.Count is 0 or > 10
            || request.RedirectUris.Any(string.IsNullOrWhiteSpace)
            || request.RedirectUris.Any(uri => uri.Length > 2048)
            || request.RedirectUris.Any(uri => !options.IsRedirectUriAllowed(uri)))
        {
            return OAuthError("invalid_client_metadata");
        }

        if (request.GrantTypes is { Count: > 0 }
            && request.GrantTypes.Any(type => type is not ("authorization_code" or "refresh_token")))
        {
            return OAuthError("invalid_client_metadata");
        }

        if (request.ResponseTypes is { Count: > 0 }
            && request.ResponseTypes.Any(type => !string.Equals(type, "code", StringComparison.Ordinal)))
        {
            return OAuthError("invalid_client_metadata");
        }

        if (request.TokenEndpointAuthMethod is not null
            && !string.Equals(request.TokenEndpointAuthMethod, "none", StringComparison.Ordinal))
        {
            return OAuthError("invalid_client_metadata");
        }

        if (request.ClientName is { Length: > 200 })
        {
            return OAuthError("invalid_client_metadata");
        }

        if (request.Scope is not null
            && request.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Any(scope => scope is not ("mcp" or "offline_access")))
        {
            return OAuthError("invalid_client_metadata");
        }

        return StatusCode(
            StatusCodes.Status201Created,
            await oauth.RegisterClientAsync(request, cancellationToken));
    }

    private string? Validate(McpAuthorizeRequest request)
    {
        if (request.ClientId.Length > 2048
            || request.RedirectUri.Length > 2048
            || request.CodeChallenge.Length > 128
            || request.Resource.Length > 2048
            || request.State is { Length: > 2048 }
            || !string.Equals(request.ResponseType, "code", StringComparison.Ordinal)
            || !string.Equals(request.CodeChallengeMethod, "S256", StringComparison.Ordinal)
            || !options.IsRedirectUriAllowed(request.RedirectUri)
            || !string.Equals(request.Resource, options.ResourceAddress, StringComparison.Ordinal))
        {
            return "invalid_request";
        }

        if (request.CodeChallenge.Any(character => character is not (>= 'A' and <= 'Z')
                and not (>= 'a' and <= 'z')
                and not (>= '0' and <= '9')
                and not '-' and not '_')
            || request.CodeChallenge.Length != 43)
        {
            return "invalid_request";
        }

        if (request.Scope is not null
            && request.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Any(scope => scope is not ("mcp" or "offline_access")))
        {
            return "invalid_scope";
        }

        return null;
    }

    private BadRequestObjectResult OAuthError(string error) =>
        BadRequest(new { error });

    private void DisableCaching()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
    }
}

/// <summary>RFC 9728 and RFC 8414 discovery documents for MCP OAuth clients.</summary>
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class McpOAuthMetadataController(McpOAuthOptions options) : ControllerBase
{
    [HttpGet("/.well-known/oauth-protected-resource")]
    [HttpGet("/mcp/.well-known/oauth-protected-resource")]
    [AllowAnonymous]
    public IActionResult ProtectedResource()
    {
        Response.Headers.CacheControl = "public, max-age=300";
        return Ok(new
        {
            resource = options.ResourceAddress,
            authorization_servers = new[] { options.AuthorizationServerBaseAddress },
            bearer_methods_supported = new[] { "header" },
            scopes_supported = new[] { "mcp" },
            resource_name = "CompanyHQ MCP",
            resource_documentation = options.PublicWebBaseAddress
        });
    }

    [HttpGet("/.well-known/oauth-authorization-server")]
    [AllowAnonymous]
    public IActionResult AuthorizationServer()
    {
        Response.Headers.CacheControl = "public, max-age=300";
        return Ok(new
        {
            issuer = options.AuthorizationServerBaseAddress,
            authorization_endpoint = options.AuthorizationEndpoint,
            token_endpoint = options.TokenEndpoint,
            registration_endpoint = options.RegistrationEndpoint,
            response_types_supported = new[] { "code" },
            grant_types_supported = new[] { "authorization_code", "refresh_token" },
            token_endpoint_auth_methods_supported = new[] { "none" },
            code_challenge_methods_supported = new[] { "S256" },
            scopes_supported = new[] { "mcp", "offline_access" },
            client_id_metadata_document_supported = false,
            authorization_response_iss_parameter_supported = false
        });
    }
}
