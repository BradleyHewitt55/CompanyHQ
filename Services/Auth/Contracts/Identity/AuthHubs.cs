namespace CompanyHQ.Services.Auth.Contracts.Identity;

/// <summary>
/// Where real-time hubs mount. Browsers cannot set an <c>Authorization</c> header on a WebSocket or
/// server-sent-events request, so under this prefix only, <c>AddAuth</c> also reads the bearer token
/// from the <c>access_token</c> query parameter. Everywhere else a query-string token is ignored, so
/// it never ends up in an API route's access log.
/// </summary>
public static class AuthHubs
{
    public const string PathPrefix = "/hubs";
}
