using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using CompanyHQ.Services.Email.Contracts.Ports;
using CompanyHQ.Services.Resend.Contracts.Models;

namespace CompanyHQ.Services.Resend.Services;

public sealed class ResendEmailProvider(
    HttpClient client,
    ResendOptions options,
    ResendRateLimiter limiter,
    ILogger<ResendEmailProvider> logger) : IEmailProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<EmailSendResult> SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await limiter.WaitAsync(cancellationToken);

            using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
            {
                Content = JsonContent.Create(new
                {
                    from = options.From,
                    to = message.To,
                    subject = message.Subject,
                    html = message.Html,
                    text = message.Text
                }, options: JsonOptions)
            };
            request.Headers.Add("Idempotency-Key", message.IdempotencyKey);

            try
            {
                using var response = await client.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadFromJsonAsync<ResendResponse>(JsonOptions, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(body?.Id))
                    {
                        return EmailSendResult.Sent(body.Id);
                    }

                    logger.LogError("Resend accepted an email request without returning an email ID.");
                    return EmailSendResult.Failed(true, "Resend did not return an email ID.");
                }

                var retryable = response.StatusCode == HttpStatusCode.TooManyRequests
                    || (int)response.StatusCode >= 500;
                if (!retryable)
                {
                    logger.LogWarning(
                        "Resend rejected an email request with status code {StatusCode}.",
                        (int)response.StatusCode);
                    return EmailSendResult.Failed(false, $"Resend rejected the email ({(int)response.StatusCode}).");
                }

                if (attempt == 2)
                {
                    return EmailSendResult.Failed(true, $"Resend was unavailable ({(int)response.StatusCode}).");
                }
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                LogRetry(exception, attempt);
                if (attempt == 2)
                {
                    logger.LogError(exception, "Resend request timed out after retries.");
                    return EmailSendResult.Failed(true, "Resend could not be reached.");
                }
            }
            catch (HttpRequestException exception) when (attempt < 2)
            {
                LogRetry(exception, attempt);
            }
            catch (HttpRequestException exception)
            {
                logger.LogError(exception, "Resend request failed after retries.");
                return EmailSendResult.Failed(true, "Resend could not be reached.");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
        }

        return EmailSendResult.Failed(true, "Resend could not send the email.");
    }

    private void LogRetry(Exception exception, int attempt) =>
        logger.LogWarning(exception, "Resend request failed on attempt {Attempt}.", attempt + 1);

    private sealed record ResendResponse(string? Id);
}
