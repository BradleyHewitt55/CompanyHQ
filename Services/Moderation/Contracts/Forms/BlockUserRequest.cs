using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Moderation.Contracts.Forms;

/// <summary>Body for creating an outbound user block.</summary>
public sealed record BlockUserRequest(
    Guid BlockedUserId,
    [MaxLength(500)] string? Reason = null);
