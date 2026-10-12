using System.Text.Json;
using CompanyHQ.Services.Notifications.Contracts.Enums;
using CompanyHQ.Services.Notifications.Contracts.Models;
using CompanyHQ.Services.Notifications.Contracts.Ports;
using CompanyHQ.Services.Persistence.Services;

namespace CompanyHQ.Services.Notifications.Services;

/// <inheritdoc cref="INotificationService" />
public sealed class NotificationService(
    IPersistence context,
    TimeProvider timeProvider) : INotificationService
{
    private const int MaxKindLength = 128;
    private const int MaxTitleLength = 255;
    private const int MaxBodyLength = 4000;

    public Task EnqueueAsync(
        NotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);

        return AddRangeAsync([request], cancellationToken);
    }

    public Task EnqueueManyAsync(
        IEnumerable<NotificationRequest> requests,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);

        var materialised = requests.ToArray();
        foreach (var request in materialised)
        {
            ArgumentNullException.ThrowIfNull(request);
            Validate(request);
        }

        return AddRangeAsync(materialised, cancellationToken);
    }

    private async Task AddRangeAsync(
        IReadOnlyList<NotificationRequest> requests,
        CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var queued = requests.Select(request => new QueuedNotification
        {
            RecipientUserId = request.RecipientUserId,
            Kind = request.Kind.Trim(),
            Title = request.Title.Trim(),
            Body = request.Body.Trim(),
            DataJson = request.Data is null
                ? null
                : JsonSerializer.Serialize(request.Data),
            Channels = request.Channels,
            Status = QueuedNotificationStatus.Pending,
            NextAttemptAt = now
        }).ToArray();

        context.AddRange(queued);
    }

    private static void Validate(NotificationRequest request)
    {
        if (request.RecipientUserId == Guid.Empty)
        {
            throw new ArgumentException("A notification recipient is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Kind) || request.Kind.Trim().Length > MaxKindLength)
        {
            throw new ArgumentException(
                $"A notification kind is required and must be at most {MaxKindLength} characters.",
                nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > MaxTitleLength)
        {
            throw new ArgumentException(
                $"A notification title is required and must be at most {MaxTitleLength} characters.",
                nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Body) || request.Body.Trim().Length > MaxBodyLength)
        {
            throw new ArgumentException(
                $"A notification body is required and must be at most {MaxBodyLength} characters.",
                nameof(request));
        }

        if ((request.Channels & ~NotificationChannels.All) != 0)
        {
            throw new ArgumentException("Unknown notification channels were requested.", nameof(request));
        }

        if (request.Data is null)
        {
            return;
        }

        foreach (var pair in request.Data)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null)
            {
                throw new ArgumentException(
                    "Notification data keys and values must be non-empty.",
                    nameof(request));
            }
        }
    }
}
