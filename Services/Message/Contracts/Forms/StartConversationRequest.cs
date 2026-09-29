using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Services.Messages.Contracts.Forms;

/// <summary>Get-or-create the caller's two-party conversation with another account.</summary>
public sealed record StartConversationRequest([Required] Guid ParticipantId);
