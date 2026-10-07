using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using CompanyHQ.Services.GitHub.Contracts.Dtos;
using CompanyHQ.Services.GitHub.Contracts.Enums;
using CompanyHQ.Services.GitHub.Contracts.Forms;
using CompanyHQ.Services.GitHub.Contracts.Models;
using CompanyHQ.Services.Postgres.Contracts;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Enums;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Postgres.Services;
using CompanyHQ.Services.Users.Services;

namespace CompanyHQ.Services.GitHub.Services;

public sealed class GitHubService(
    IPostgresService<GitHubConnection> connections,
    IUserAuthorizationService authorization,
    IHttpClientFactory clients,
    GitHubOptions options) : IGitHubService
{
    public const string HttpClientName = "github";

    private sealed record Connection(string Repository, string Token);

    private const int NonceLength = 12;
    private const int TagLength = 16;

    public async Task<ClientGitHubConnectionDto> GetAsync(
        ResponseAudience audience = ResponseAudience.Client,
        CancellationToken cancellationToken = default)
    {
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

        using var issue = Request(HttpMethod.Post, $"repos/{connection.Repository}/issues", connection.Token);
        issue.Content = JsonContent.Create(new
        {
            title = $"[{request.Category.Trim()}] {request.Title.Trim()}",
            body = body.ToString()
        });

        var (error, _) = await SendAsync(issue, cancellationToken);
        return error;
    }

    public async Task<(GitHubError Error, PagedResult<AdminGitHubIssueDto>? Issues)> ListIssuesAsync(
        Guid actingUserId,
        PageRequest paging,
        string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paging);

        var (denied, connection) = await ManagedConnectionAsync(actingUserId, cancellationToken);
        if (connection is null)
        {
            return (denied, null);
        }

        // The search API is the one issue listing that carries a total and leaves pull requests out.
        var terms = $"repo:{connection.Repository} is:issue"
            + (string.IsNullOrWhiteSpace(keyword) ? string.Empty : $" {keyword.Trim()}");
        using var request = Request(
            HttpMethod.Get,
            $"search/issues?q={Uri.EscapeDataString(terms)}&sort=updated&order=desc&per_page={paging.PageSize}&page={paging.Page}",
            connection.Token);

        var (error, json) = await SendAsync(request, cancellationToken);
        if (json is null)
        {
            return (error, null);
        }

        using (json)
        {
            var items = json.RootElement.GetProperty("items").EnumerateArray().Select(Issue).ToList();
            return (GitHubError.None, new PagedResult<AdminGitHubIssueDto>(
                items, paging.Page, paging.PageSize, json.RootElement.GetProperty("total_count").GetInt32()));
        }
    }

    public async Task<(GitHubError Error, AdminGitHubIssueThreadDto? Thread)> GetIssueAsync(
        Guid actingUserId,
        int number,
        CancellationToken cancellationToken = default)
    {
        var (denied, connection) = await ManagedConnectionAsync(actingUserId, cancellationToken);
        if (connection is null)
        {
            return (denied, null);
        }

        using var issueRequest = Request(HttpMethod.Get, $"repos/{connection.Repository}/issues/{number}", connection.Token);
        var (issueError, issueJson) = await SendAsync(issueRequest, cancellationToken);
        if (issueJson is null)
        {
            return (issueError, null);
        }

        using (issueJson)
        {
            // A pull request answers on the issues route too; this service only deals in issues.
            if (issueJson.RootElement.TryGetProperty("pull_request", out _))
            {
                return (GitHubError.NotFound, null);
            }

            using var commentsRequest = Request(
                HttpMethod.Get,
                $"repos/{connection.Repository}/issues/{number}/comments?per_page=100",
                connection.Token);
            var (commentsError, commentsJson) = await SendAsync(commentsRequest, cancellationToken);
            if (commentsJson is null)
            {
                return (commentsError, null);
            }

            using (commentsJson)
            {
                var comments = commentsJson.RootElement.EnumerateArray()
                    .Select(comment => new AdminGitHubIssueCommentDto(
                        comment.GetProperty("id").GetInt64(),
                        Text(comment, "body"),
                        Login(comment),
                        comment.GetProperty("created_at").GetDateTimeOffset()))
                    .ToList();
                return (GitHubError.None, new AdminGitHubIssueThreadDto(Issue(issueJson.RootElement), comments));
            }
        }
    }

    public async Task<GitHubError> CommentAsync(
        Guid actingUserId,
        int number,
        string body,
        CancellationToken cancellationToken = default)
    {
        var (denied, connection) = await ManagedConnectionAsync(actingUserId, cancellationToken);
        if (connection is null)
        {
            return denied;
        }

        using var request = Request(HttpMethod.Post, $"repos/{connection.Repository}/issues/{number}/comments", connection.Token);
        request.Content = JsonContent.Create(new { body = body.Trim() });
        var (error, json) = await SendAsync(request, cancellationToken);
        json?.Dispose();
        return error;
    }

    public async Task<GitHubError> CloseIssueAsync(
        Guid actingUserId,
        int number,
        CancellationToken cancellationToken = default)
    {
        var (denied, connection) = await ManagedConnectionAsync(actingUserId, cancellationToken);
        if (connection is null)
        {
            return denied;
        }

        using var request = Request(HttpMethod.Patch, $"repos/{connection.Repository}/issues/{number}", connection.Token);
        request.Content = JsonContent.Create(new { state = "closed" });
        var (error, json) = await SendAsync(request, cancellationToken);
        json?.Dispose();
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

    /// <summary>The parsed body on success; the caller disposes it.</summary>
    private async Task<(GitHubError Error, JsonDocument? Json)> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await clients.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
            if (response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Gone)
            {
                return (GitHubError.NotFound, null);
            }

            if (!response.IsSuccessStatusCode)
            {
                return (GitHubError.Rejected, null);
            }

            return (GitHubError.None, await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken));
        }
        catch (Exception exception) when (IsTransportFailure(exception, cancellationToken) || exception is JsonException)
        {
            return (GitHubError.Unavailable, null);
        }
    }

    private static AdminGitHubIssueDto Issue(JsonElement issue) =>
        new(
            issue.GetProperty("number").GetInt32(),
            Text(issue, "title"),
            Text(issue, "body"),
            Login(issue),
            string.Equals(Text(issue, "state"), "open", StringComparison.Ordinal),
            Text(issue, "html_url"),
            issue.GetProperty("comments").GetInt32(),
            issue.GetProperty("created_at").GetDateTimeOffset(),
            issue.GetProperty("updated_at").GetDateTimeOffset());

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.String
            ? value.GetString()!
            : string.Empty;

    private static string Login(JsonElement element) =>
        element.TryGetProperty("user", out var user) && user.ValueKind is JsonValueKind.Object
            ? Text(user, "login")
            : string.Empty;

    private Task<bool> CanManageAsync(Guid userId, CancellationToken cancellationToken) =>
        authorization.HasPermissionAsync(userId, AccessPermissions.ManageGitHub, cancellationToken);

    /// <summary>GitHub's own spelling of the repository, or null when the token cannot file issues in it.</summary>
    private async Task<string?> VerifyAsync(string repository, string accessToken, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Get, $"repos/{repository}", accessToken);
        using var response = await clients.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var details = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);
        var root = details.RootElement;

        return root.TryGetProperty("full_name", out var fullName)
               && string.Equals(fullName.GetString(), repository, StringComparison.OrdinalIgnoreCase)
               && root.TryGetProperty("has_issues", out var hasIssues)
               && hasIssues.GetBoolean()
               && root.TryGetProperty("permissions", out var permissions)
               && permissions.TryGetProperty("pull", out var pull)
               && pull.GetBoolean()
            ? fullName.GetString()
            : null;
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, new Uri($"https://api.github.com/{path}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.ParseAdd("CompanyHQ/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return request;
    }

    private static bool IsTransportFailure(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException
        || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested);

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
