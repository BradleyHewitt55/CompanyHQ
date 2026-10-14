using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Messages.Contracts.Forms;

/// <summary>Get-or-create the caller's two-party conversation with another account.</summary>
public sealed record StartConversationRequest(
    [Required] Guid ParticipantId,
    [MaxLength(64), RegularExpression("^[a-zA-Z0-9_-]+$")] string? ContextKind = null,
    [MaxLength(160), RegularExpression(@".*\S.*")] string? ContextId = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (ContextKind is not null && (ContextKind.Length is < 1 or > 64 || ContextKind.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '_' and not '-')))
            yield return new ValidationResult("ContextKind must contain 1 to 64 letters, numbers, underscores or hyphens.", [nameof(ContextKind)]);
        if (ContextId is not null && (string.IsNullOrWhiteSpace(ContextId) || ContextId.Length > 160))
            yield return new ValidationResult("ContextId must contain 1 to 160 characters.", [nameof(ContextId)]);
        if ((ContextKind is null) != (ContextId is null))
            yield return new ValidationResult("ContextKind and ContextId must be supplied together.", [nameof(ContextKind), nameof(ContextId)]);
    }
}
