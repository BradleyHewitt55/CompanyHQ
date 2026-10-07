using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Services.Widget.Contracts.Forms;

/// <summary>Body for <c>POST /api/widget/conversations</c>. Anonymous public input.</summary>
/// <param name="Source">One of the deployment's allowed sources; the first of them when left out.</param>
/// <param name="Origin">The page or screen the widget was opened on.</param>
public sealed record StartWidgetConversationRequest(
    [Required, StringLength(120, MinimumLength = 2)] string Name,
    [Required, EmailAddress, StringLength(254)] string Email,
    [Required, StringLength(4000, MinimumLength = 10)] string Message,
    [StringLength(40)] string? Source = null,
    [StringLength(600)] string? Origin = null);

/// <summary>The visitor proving a conversation is theirs.</summary>
public sealed record WidgetConversationAccessRequest(
    [Required, RegularExpression("^[A-F0-9]{64}$")] string Token);

public sealed record SendWidgetMessageRequest(
    [Required, RegularExpression("^[A-F0-9]{64}$")] string Token,
    [Required, StringLength(4000, MinimumLength = 1)] string Body);

/// <summary>Body for <c>POST /api/widget/issues</c>. Anonymous public input; the email is never an identity.</summary>
public sealed record SubmitWidgetIssueRequest(
    [Required, RegularExpression("^(Bug|Feature|Security|Other)$")] string Category,
    [Required, StringLength(160, MinimumLength = 5)] string Title,
    [Required, StringLength(4000, MinimumLength = 10)] string Message,
    [EmailAddress, StringLength(254)] string? Email = null,
    [StringLength(40)] string? Source = null,
    [StringLength(600)] string? Origin = null);
