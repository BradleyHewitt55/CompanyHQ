using CompanyHQ.ServiceCatalog.Contracts;
using System.Net;
using System.Security.Cryptography;
using System.Text;

using Octokit;
using Octokit.Internal;

using CompanyHQ.GitHub.Contracts.Dtos;
using CompanyHQ.GitHub.Contracts.Enums;
using CompanyHQ.GitHub.Contracts.Forms;
using CompanyHQ.GitHub.Contracts.Models;
using CompanyHQ.Postgres.Contracts;
using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts.Enums;
using CompanyHQ.Postgres.Contracts.Forms;
using CompanyHQ.Persistence.Services;
using CompanyHQ.Users.Services;

namespace CompanyHQ.GitHub.Services;

public sealed class GitHubService(IServiceAvailability? availability,
    IRepository<GitHubConnection> connections,
    IUserAuthorizationService authorization,
    IHttpMessageHandlerFactory handlers,
    GitHubOptions options) : IGitHubService
{
    /// <summary>Retains the original strict constructor for existing consumers.</summary>
    public GitHubService(
    IRepository<GitHubConnection> connections,
    IUserAuthorizationService authorization,
    IHttpMessageHandlerFactory handlers,
    GitHubOptions options) : this(null, connections, authorization, handlers, options) { }

    public const string HttpClientName = "github";

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    private sealed record Connection(string Repository, string Token)
    {
        public string Owner => Repository[..Repository.IndexOf('/')];

        public string Name => Repository[(Repository.IndexOf('/') + 1)..];
    }

    private const int NonceLength = 12;
    private const int TagLength = 16;

    public async Task<ClientGitHubConnectionDto> GetAsync(
        ResponseAudience audience = ResponseAudience.Client,
        CancellationToken cancellationToken = default)
    {
        await (availability?.RequireAsync("github", false, cancellationToken) ?? Task.CompletedTask);
        var connection = await connections.Query()
            .Where(candidate => candidate.Id == GitHubConnection.SingletonId)
            .Select(candidate => new { candidate.Repository, candidate.UpdatedAt })
            .FirstOrDefaultAsync(cancellationToken);
        var enabled = connection is not null && options.IsConfigured;

        return audience is ResponseAudience.Admin
            ? new AdminGitHubConnectionDto(enabled, connection?.Repository, connection?.UpdatedAt)
            : new ClientGitHubConnectionDto(enabled);
    }

    public async Task<GitHubError> ConnectAsync(
        Guid actingUserId,
        ConnectGitHubRepositoryRequest request,
        CancellationToken cancellationToken = default)
    {
        await (availability?.RequireAsync("github", false, cancellationToken) ?? Task.CompletedTask);
        ArgumentNullException.ThrowIfNull(request);

        if (!await CanManageAsync(actingUserId, cancellationToken))
        {
            return GitHubError.Forbidden;
        }

        if (!options.IsConfigured)
        {
            return GitHubError.NotConfigured;
        }

        string? repository;
        try
        {
            repository = await VerifyAsync(request.Repository, request.AccessToken, cancellationToken);
        }
        catch (Exception exception) when (IsTransportFailure(exception, cancellationToken))
        {
            return GitHubError.Unavailable;
        }

        if (repository is null)
        {
            return GitHubError.Rejected;
        }

        var token = Encrypt(request.AccessToken);
        var existing = await connections.GetAsync(GitHubConnection.SingletonId, cancellationToken);
        if (existing is null)
        {
            await connections.AddAsync(
                new GitHubConnection
                {
                    Id = GitHubConnection.SingletonId,
                    Repository = repository,
                    TokenEncrypted = token
                },
                cancellationToken);
        }
        else
        {
            existing.Repository = repository;
            existing.TokenEncrypted = token;
            await connections.UpdateAsync(existing, cancellationToken);
        }

        return GitHubError.None;
    }

    public async Task<GitHubError> DisconnectAsync(Guid actingUserId, CancellationToken cancellationToken = default)
    {
        await (availability?.RequireAsync("github", false, cancellationToken) ?? Task.CompletedTask);
        if (!await CanManageAsync(actingUserId, cancellationToken))
        {
            return GitHubError.Forbidden;
        }

        await connections.DeleteAsync(GitHubConnection.SingletonId, cancellationToken);
        return GitHubError.None;
    }

    public async Task<GitHubError> SubmitIssueAsync(
        SubmitGitHubIssueRequest request,
        CancellationToken cancellationToken = default)
    {
        await (availability?.RequireAsync("github", false, cancellationToken) ?? Task.CompletedTask);
        ArgumentNullException.ThrowIfNull(request);

        if (await ConnectionAsync(cancellationToken) is not { } connection)
        {
            return GitHubError.NotConnected;
        }

        var body = new StringBuilder(request.Message.Trim());
        if (!string.IsNullOrWhiteSpace(request.Origin))
        {
            body.Append("\n\nReported from: ").Append(request.Origin.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            body.Append("\nContact email: ").Append(request.Email.Trim());
        }

        var issue = new NewIssue($"[{request.Category.Trim()}] {request.Title.Trim()}") { Body = body.ToString() };
        var (error, _) = await SendAsync(
            connection,
            client => client.Issue.Create(connection.Owner, connection.Name, issue),
            cancellationToken);
        return error;
    }

    public async Task<(GitHubError Error, PagedResult<AdminGitHubIssueDto>? Issues)> ListIssuesAsync(
        Guid actingUserId,
        PageRequest paging,
        string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        await (availability?.RequireAsync("github", false, cancellationToken) ?? Task.CompletedTask);
        ArgumentNullException.ThrowIfNull(paging);

        var (denied, connection) = await ManagedConnectionAsync(actingUserId, cancellationToken);
        if (connection is null)
        {
            return (denied, null);
        }

        // The search API is the one issue listing that carries a total and leaves pull requests out.
        var search = string.IsNullOrWhiteSpace(keyword) ? new SearchIssuesRequest() : new SearchIssuesRequest(keyword.Trim());
        search.Repos.Add(connection.Owner, connection.Name);
        search.Type = IssueTypeQualifier.Issue;
        search.SortField = IssueSearchSort.Updated;
        search.Order = Octokit.SortDirection.Descending;
        search.PerPage = paging.PageSize;
        search.Page = paging.Page;

        var (error, found) = await SendAsync(connection, client => client.Search.SearchIssues(search), cancellationToken);
        return found is null
            ? (error, null)
            : (GitHubError.None, new PagedResult<AdminGitHubIssueDto>(
                found.Items.Select(Map).ToList(), paging.Page, paging.PageSize, found.TotalCount));
    }

    public async Task<(GitHubError Error, AdminGitHubIssueThreadDto? Thread)> GetIssueAsync(
        Guid actingUserId,
        int number,
        CancellationToken cancellationToken = default)
    {
        await (availability?.RequireAsync("github", false, cancellationToken) ?? Task.CompletedTask);
        var (denied, connection) = await ManagedConnectionAsync(actingUserId, cancellationToken);
        if (connection is null)
        {
            return (denied, null);
        }

        var (issueError, issue) = await SendAsync(
            connection,
            client => client.Issue.Get(connection.Owner, connection.Name, number),
            cancellationToken);
        if (issue is null)
        {
            return (issueError, null);
        }

        // A pull request answers on the issues route too; this service only deals in issues.
        if (issue.PullRequest is not null)
        {
            return (GitHubError.NotFound, null);
        }

        var firstPage = new ApiOptions { PageSize = 100, PageCount = 1 };
        var (commentsError, comments) = await SendAsync(
            connection,
            client => client.Issue.Comment.GetAllForIssue(connection.Owner, connection.Name, number, firstPage),
            cancellationToken);
        return comments is null
            ? (commentsError, null)
            : (GitHubError.None, new AdminGitHubIssueThreadDto(
                Map(issue),
                comments
                    .Select(comment => new AdminGitHubIssueCommentDto(
                        comment.Id,
                        comment.Body ?? string.Empty,
                        comment.User?.Login ?? string.Empty,
                        comment.CreatedAt))
                    .ToList()));
    }

    public async Task<GitHubError> CommentAsync(
        Guid actingUserId,
        int number,
        string body,
        CancellationToken cancellationToken = default)
    {
        await (availability?.RequireAsync("github", false, cancellationToken) ?? Task.CompletedTask);
        var (denied, connection) = await ManagedConnectionAsync(actingUserId, cancellationToken);
        if (connection is null)
        {
            return denied;
        }

        var (error, _) = await SendAsync(
            connection,
            client => client.Issue.Comment.Create(connection.Owner, connection.Name, number, body.Trim()),
            cancellationToken);
        return error;
    }

    public async Task<GitHubError> CloseIssueAsync(
        Guid actingUserId,
        int number,
        CancellationToken cancellationToken = default)
    {
        await (availability?.RequireAsync("github", false, cancellationToken) ?? Task.CompletedTask);
        var (denied, connection) = await ManagedConnectionAsync(actingUserId, cancellationToken);
        if (connection is null)
        {
            return denied;
        }

        // Built from the issue as it stands: a bare IssueUpdate always sends a null milestone, which
        // would clear the one the issue has.
        var (readError, issue) = await SendAsync(
            connection,
            client => client.Issue.Get(connection.Owner, connection.Name, number),
            cancellationToken);
        if (issue is null)
        {
            return readError;
        }

        var closed = issue.ToUpdate();
        closed.State = ItemState.Closed;
        var (error, _) = await SendAsync(
            connection,
            client => client.Issue.Update(connection.Owner, connection.Name, number, closed),
            cancellationToken);
        return error;
    }

    private async Task<(GitHubError Error, Connection? Connection)> ManagedConnectionAsync(
        Guid actingUserId,
        CancellationToken cancellationToken)
    {
        if (!await CanManageAsync(actingUserId, cancellationToken))
        {
            return (GitHubError.Forbidden, null);
        }

        return await ConnectionAsync(cancellationToken) is { } connection
            ? (GitHubError.None, connection)
            : (GitHubError.NotConnected, null);
    }

    /// <summary>The repository and its decrypted token, or null when nothing usable is connected.</summary>
    private async Task<Connection?> ConnectionAsync(CancellationToken cancellationToken)
    {
        var stored = options.IsConfigured
            ? await connections.GetAsync(GitHubConnection.SingletonId, cancellationToken)
            : null;
        if (stored is null)
        {
            return null;
        }

        try
        {
            return new Connection(stored.Repository, Decrypt(stored.TokenEncrypted));
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or ArgumentException)
        {
            // The key was rotated or the row predates it. Only a reconnection can repair that.
            return null;
        }
    }

    /// <summary>GitHub's answer on success; null with the reason otherwise.</summary>
    private async Task<(GitHubError Error, T? Result)> SendAsync<T>(
        Connection connection,
        Func<GitHubClient, Task<T>> call,
        CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            return (GitHubError.None, await call(Client(connection.Token)).WaitAsync(cancellationToken));
        }
        catch (ApiException exception) when (exception.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return (GitHubError.NotFound, null);
        }
        catch (ApiException)
        {
            return (GitHubError.Rejected, null);
        }
        catch (Exception exception) when (IsTransportFailure(exception, cancellationToken))
        {
            return (GitHubError.Unavailable, null);
        }
    }

    private static AdminGitHubIssueDto Map(Issue issue) =>
        new(
            issue.Number,
            issue.Title ?? string.Empty,
            issue.Body ?? string.Empty,
            issue.User?.Login ?? string.Empty,
            issue.State.Value is ItemState.Open,
            issue.HtmlUrl ?? string.Empty,
            issue.Comments,
            issue.CreatedAt,
            issue.UpdatedAt ?? issue.CreatedAt);

    private Task<bool> CanManageAsync(Guid userId, CancellationToken cancellationToken) =>
        authorization.HasPermissionAsync(userId, AccessPermissions.ManageGitHub, cancellationToken);

    /// <summary>GitHub's own spelling of the repository, or null when the token cannot file issues in it.</summary>
    private async Task<string?> VerifyAsync(string repository, string accessToken, CancellationToken cancellationToken)
    {
        var candidate = new Connection(repository, accessToken);
        Repository details;
        try
        {
            details = await Client(accessToken).Repository.Get(candidate.Owner, candidate.Name).WaitAsync(cancellationToken);
        }
        catch (ApiException)
        {
            return null;
        }

        return string.Equals(details.FullName, repository, StringComparison.OrdinalIgnoreCase)
               && details.HasIssues
               && details.Permissions?.Pull is true
            ? details.FullName
            : null;
    }

    /// <summary>
    /// GitHub's own client for one stored token. The handler comes from the host's pool, so a client
    /// per call costs no socket.
    /// </summary>
    private GitHubClient Client(string token)
    {
        var client = new GitHubClient(new Octokit.Connection(
            new ProductHeaderValue("CompanyHQ", "1.0"),
            GitHubClient.GitHubApiUrl,
            new InMemoryCredentialStore(new Credentials(token)),
            new HttpClientAdapter(() => handlers.CreateHandler(HttpClientName)),
            new SimpleJsonSerializer()));
        client.SetRequestTimeout(RequestTimeout);
        return client;
    }

    private static bool IsTransportFailure(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException
        || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);

    // AES-GCM, stored as nonce + tag + ciphertext.
    private string Encrypt(string token)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        var plaintext = Encoding.UTF8.GetBytes(token);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagLength];
        using (var aes = new AesGcm(options.Key(), TagLength))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        return Convert.ToBase64String([.. nonce, .. tag, .. ciphertext]);
    }

    private string Decrypt(string encoded)
    {
        var payload = Convert.FromBase64String(encoded);
        var plaintext = new byte[payload.Length - NonceLength - TagLength];
        using (var aes = new AesGcm(options.Key(), TagLength))
        {
            aes.Decrypt(
                payload[..NonceLength],
                payload[(NonceLength + TagLength)..],
                payload[NonceLength..(NonceLength + TagLength)],
                plaintext);
        }

        return Encoding.UTF8.GetString(plaintext);
    }
}
