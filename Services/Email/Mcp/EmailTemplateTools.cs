using System.ComponentModel;

using Microsoft.AspNetCore.Http;

using ModelContextProtocol.Server;

using CompanyHQ.Email.Contracts.Dtos;
using CompanyHQ.Email.Contracts.Forms;
using CompanyHQ.Email.Services;
using CompanyHQ.Postgres.Contracts;
using CompanyHQ.Auth.Contracts.Identity;

namespace CompanyHQ.Email.Mcp;

[McpServerToolType]
public sealed class EmailTemplateTools(IEmailTemplateService templates, IHttpContextAccessor httpContextAccessor)
{
    [McpServerTool(Name = "list_email_templates")]
    public Task<IReadOnlyList<AdminEmailTemplateDto>> ListEmailTemplates(CancellationToken cancellationToken = default)
    {
        return templates.ListAsync(cancellationToken);
    }

    [McpServerTool(Name = "get_email_template")]
    public Task<AdminEmailTemplateDto?> GetEmailTemplate(
        [Description("The email template key.")] string key,
        CancellationToken cancellationToken = default)
    {
        return templates.GetAsync(key, cancellationToken);
    }

    [McpServerTool(Name = "update_email_template")]
    public async Task<AdminEmailTemplateDto> UpdateEmailTemplate(
        [Description("The email template key.")] string key,
        [Description("The replacement template HTML.")] UpdateEmailTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        return await templates.UpdateAsync(key, request.Html, CurrentUserId(), cancellationToken);
    }

    [McpServerTool(Name = "preview_email_template")]
    public Task<AdminEmailTemplatePreviewDto> PreviewEmailTemplate(
        [Description("The email template key.")] string key,
        [Description("The HTML and placeholder values to preview.")] PreviewEmailTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        return templates.PreviewAsync(
            key,
            request.Html,
            request.Values?.ToDictionary(pair => pair.Key, pair => (string?)pair.Value)
                ?? new Dictionary<string, string?>(),
            cancellationToken);
    }

    [McpServerTool(Name = "reset_email_template")]
    public Task<AdminEmailTemplateDto> ResetEmailTemplate(
        [Description("The email template key.")] string key,
        CancellationToken cancellationToken = default)
    {
        return templates.ResetAsync(key, cancellationToken);
    }

    // The MCP call filter has already checked this tool's capability and the key's write access
    // before the call reaches here, so an adapter only needs to know who is calling.
    private Guid? CurrentUserId() => httpContextAccessor.HttpContext?.User.GetUserId();
}
