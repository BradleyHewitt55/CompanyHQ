namespace CompanyHQ.Services.GitHub.Contracts.Enums;

/// <summary>Why a GitHub operation did not complete.</summary>
public enum GitHubError
{
    None,

    /// <summary>The caller's role does not grant <c>github.manage</c>.</summary>
    Forbidden,

    /// <summary>No token encryption key is configured on this deployment.</summary>
    NotConfigured,

    /// <summary>No repository is connected, or its stored token can no longer be read.</summary>
    NotConnected,

    /// <summary>The connected repository has no issue with that number.</summary>
    NotFound,

    /// <summary>GitHub answered, and refused the repository or the issue.</summary>
    Rejected,

    /// <summary>GitHub could not be reached in time.</summary>
    Unavailable
}
