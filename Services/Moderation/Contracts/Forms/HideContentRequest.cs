namespace CompanyHQ.Services.Moderation.Contracts.Forms;

/// <summary>Internal command used by report and block actions to add a hidden target.</summary>
public sealed record HideContentRequest(string TargetKind, Guid TargetId);
