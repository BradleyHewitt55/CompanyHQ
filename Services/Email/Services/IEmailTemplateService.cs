using CompanyHQ.Services.Email.Contracts.Dtos;
using CompanyHQ.Services.Email.Contracts.Ports;

namespace CompanyHQ.Services.Email.Services;

/// <summary>
/// Template management for the admin routes and MCP tools, plus the <see cref="IEmailTemplateSender"/>
/// every other service sends through.
/// </summary>
public interface IEmailTemplateService : IEmailTemplateSender
{
    Task<AdminEmailTemplatePreviewDto> RenderAsync(
        string templateKey,
        IReadOnlyDictionary<string, string?> values,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminEmailTemplateDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<AdminEmailTemplateDto?> GetAsync(
        string templateKey,
        CancellationToken cancellationToken = default);

    Task<AdminEmailTemplateDto> UpdateAsync(
        string templateKey,
        string html,
        Guid? updatedByUserId,
        CancellationToken cancellationToken = default);

    Task<AdminEmailTemplatePreviewDto> PreviewAsync(
        string templateKey,
        string html,
        IReadOnlyDictionary<string, string?> values,
        CancellationToken cancellationToken = default);

    Task<AdminEmailTemplateDto> ResetAsync(
        string templateKey,
        CancellationToken cancellationToken = default);
}
