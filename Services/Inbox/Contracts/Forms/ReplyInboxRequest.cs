using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Inbox.Contracts.Forms;

/// <summary>Body for <c>POST /api/inbox/{channel}/{id}/replies</c>.</summary>
public sealed record ReplyInboxRequest(
    [Required, StringLength(4000, MinimumLength = 1)] string Body);
