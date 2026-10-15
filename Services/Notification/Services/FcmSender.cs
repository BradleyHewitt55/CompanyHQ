using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;

using CompanyHQ.Notifications.Contracts.Ports;
using CompanyHQ.ServiceCatalog.Contracts;

namespace CompanyHQ.Notifications.Services;

/// <summary>Notification's push port, implemented with data-only Firebase Cloud Messaging multicasts.</summary>
internal sealed class FcmSender(
    FirebaseCredentialProvider credentials,
    ILogger<FcmSender> logger, IServiceAvailability? availability = null) : IPushSender
{
    private const int MaxTokensPerRequest = 500;
    private const string TitleKey = "title";
    private const string BodyKey = "body";

    public bool IsConfigured => credentials.IsConfigured;

    public async Task<PushSendResult> SendAsync(
        PushMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        Validate(message);

        if (!IsConfigured || credentials.App is null)
        {
            return new PushSendResult(
                message.Tokens
                    .Select(token => new PushTokenOutcome(token, PushDeliveryStatus.PermanentFailure))
                    .ToArray());
        }

        var data = BuildData(message);
        var outcomes = new List<PushTokenOutcome>(message.Tokens.Count);
        FirebaseMessaging messaging;
        try
        {
            messaging = FirebaseMessaging.GetMessaging(credentials.App);
        }
        catch (Exception exception)
        {
            var status = MapFailure(exception);
            logger.LogWarning("FCM client setup failed with {DeliveryStatus}.", status);
            return new PushSendResult(
                message.Tokens
                    .Select(token => new PushTokenOutcome(token, status))
                    .ToArray());
        }

        foreach (var tokenChunk in message.Tokens.Chunk(MaxTokensPerRequest))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (availability is not null) await availability.RequireAsync("notifications", true, cancellationToken);
            var tokens = tokenChunk.ToArray();

            try
            {
                var response = await messaging.SendEachForMulticastAsync(
                    new MulticastMessage
                    {
                        // FirebaseAdmin 3.6 marks FCM registration tokens obsolete in favour of
                        // installation IDs, but the mobile contract persists registration tokens.
                        // Sending them as FIDs would make every delivery fail, so keep this exact,
                        // dependency-bound exception until the contract migrates end-to-end.
#pragma warning disable CS0618
                        Tokens = tokens,
#pragma warning restore CS0618
                        Data = data
                    },
                    cancellationToken);

                for (var index = 0; index < tokens.Length; index++)
                {
                    var sendResponse = response.Responses[index];
                    outcomes.Add(new PushTokenOutcome(
                        tokens[index],
                        sendResponse.IsSuccess
                            ? PushDeliveryStatus.Delivered
                            : MapFailure(sendResponse.Exception)));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                outcomes.AddRange(tokens.Select(token =>
                    new PushTokenOutcome(token, PushDeliveryStatus.TransientFailure)));
            }
            catch (Exception exception)
            {
                var status = MapFailure(exception);
                logger.LogWarning("FCM multicast failed with {DeliveryStatus}.", status);
                outcomes.AddRange(tokens.Select(token => new PushTokenOutcome(token, status)));
            }
        }

        return new PushSendResult(outcomes);
    }

    private static IReadOnlyDictionary<string, string> BuildData(PushMessage message)
    {
        var data = message.Data is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(message.Data, StringComparer.Ordinal);

        // Title and body are data fields on purpose. Setting MulticastMessage.Notification would
        // make Android render in the background and skip the app's deep-link code path.
        data[TitleKey] = message.Title;
        data[BodyKey] = message.Body;

        return data;
    }

    private static void Validate(PushMessage message)
    {
        ArgumentNullException.ThrowIfNull(message.Tokens);

        if (message.Tokens.Count == 0 || message.Tokens.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("An FCM message needs at least one non-empty token.", nameof(message));
        }

        if (string.IsNullOrWhiteSpace(message.Title))
        {
            throw new ArgumentException("An FCM message needs a title.", nameof(message));
        }

        if (string.IsNullOrWhiteSpace(message.Body))
        {
            throw new ArgumentException("An FCM message needs a body.", nameof(message));
        }

        if (message.Data is not null
            && message.Data.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null))
        {
            throw new ArgumentException("FCM data keys and values must be non-empty.", nameof(message));
        }
    }

    private static PushDeliveryStatus MapFailure(Exception? exception)
    {
        if (exception is null)
        {
            return PushDeliveryStatus.PermanentFailure;
        }

        if (exception is FirebaseMessagingException messagingException)
        {
            if (messagingException.MessagingErrorCode == MessagingErrorCode.Unregistered
                || messagingException.ErrorCode == ErrorCode.NotFound
                || messagingException.HttpResponse?.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return PushDeliveryStatus.TokenDead;
            }

            if (messagingException.HttpResponse?.IsSuccessStatusCode == false
                && (int)messagingException.HttpResponse.StatusCode >= 500)
            {
                return PushDeliveryStatus.TransientFailure;
            }

            if (messagingException.MessagingErrorCode is MessagingErrorCode.Internal
                or MessagingErrorCode.Unavailable
                || messagingException.ErrorCode is ErrorCode.Internal
                or ErrorCode.Unavailable
                or ErrorCode.DeadlineExceeded)
            {
                return PushDeliveryStatus.TransientFailure;
            }
        }

        if (HasTransientTransportException(exception))
        {
            return PushDeliveryStatus.TransientFailure;
        }

        return PushDeliveryStatus.PermanentFailure;
    }

    private static bool HasTransientTransportException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is HttpRequestException
                or IOException
                or TimeoutException
                or TaskCanceledException)
            {
                return true;
            }
        }

        return false;
    }
}
