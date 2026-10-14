namespace CompanyHQ.Email.Contracts.Dtos;

/// <summary>A rendered administrator preview. It is never sent to a provider.</summary>
public sealed record AdminEmailTemplatePreviewDto(string Subject, string Html, string Text);
