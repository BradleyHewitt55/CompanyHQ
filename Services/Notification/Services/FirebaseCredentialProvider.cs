using System.Text.Json;

using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;

using CompanyHQ.Notifications.Contracts.Models;

namespace CompanyHQ.Notifications.Services;

/// <summary>Loads the optional service account and owns the named Firebase app.</summary>
internal sealed class FirebaseCredentialProvider
{
    private const string AppName = "CompanyHQ";

    public FirebaseCredentialProvider(
        FirebaseOptions options,
        ILogger<FirebaseCredentialProvider> logger,
        FirebaseRegistrationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        if (string.IsNullOrWhiteSpace(options.ProjectId)
            || string.IsNullOrWhiteSpace(options.ServiceAccountJson))
        {
            logger.LogWarning(
                "Firebase push delivery is disabled because FCM credentials are not configured.");
            return;
        }

        try
        {
            var json = ReadCredentialJson(options.ServiceAccountJson);
            if (json is null || !MatchesProject(json, options.ProjectId))
            {
                if (policy.StrictCredentials) throw new InvalidOperationException("Configured Firebase credentials are invalid.");
                logger.LogWarning(
                    "Firebase push delivery is disabled because the FCM credentials are invalid.");
                return;
            }

            var credential = CredentialFactory.FromJson<ServiceAccountCredential>(json).ToGoogleCredential();
            App = GetExistingApp() ?? FirebaseApp.Create(new AppOptions
            {
                Credential = credential,
                ProjectId = options.ProjectId
            }, AppName);
        }
        catch (Exception)
        {
            if (policy.StrictCredentials) throw new InvalidOperationException("Configured Firebase credentials are invalid.");
            // Credential contents and paths stay out of logs; the startup warning says enough for
            // an operator to inspect configuration without leaking a private key.
            logger.LogWarning(
                "Firebase push delivery is disabled because the FCM credentials are invalid.");
        }
    }

    public bool IsConfigured => App is not null;

    public FirebaseApp? App { get; }

    internal static void ValidateConfigured(FirebaseOptions options)
    {
        try
        {
            var json = ReadCredentialJson(options.ServiceAccountJson);
            if (json is null || !MatchesProject(json, options.ProjectId))
                throw new InvalidOperationException();
            _ = CredentialFactory.FromJson<ServiceAccountCredential>(json).ToGoogleCredential();
        }
        catch (Exception)
        {
            throw new InvalidOperationException("Configured Firebase credentials are invalid.");
        }
    }

    private static FirebaseApp? GetExistingApp()
    {
        try
        {
            return FirebaseApp.GetInstance(AppName);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string? ReadCredentialJson(string configuredValue)
    {
        var value = configuredValue.Trim();
        if (value.StartsWith('{'))
        {
            return value;
        }

        var path = Path.IsPathRooted(value)
            ? value
            : Path.Combine(FindRepositoryRoot(), value);

        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, ".env"))
                || File.Exists(Path.Combine(directory.FullName, "CompanyHQ.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static bool MatchesProject(string json, string projectId)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("project_id", out var keyProject)
            && string.Equals(keyProject.GetString(), projectId, StringComparison.Ordinal);
    }
}

internal sealed record FirebaseRegistrationPolicy(bool StrictCredentials);
