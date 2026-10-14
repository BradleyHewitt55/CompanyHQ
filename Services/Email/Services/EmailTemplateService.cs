using CompanyHQ.Email.Contracts.Dtos;
using CompanyHQ.Email.Contracts.Models;
using CompanyHQ.Email.Contracts.Ports;

namespace CompanyHQ.Email.Services;

public sealed class EmailTemplateService(
    IEmailTemplateStore store,
    IEmailService email) : IEmailTemplateService
{
    public async Task<AdminEmailTemplatePreviewDto> RenderAsync(
        string templateKey,
        IReadOnlyDictionary<string, string?> values,
        CancellationToken cancellationToken = default)
    {
        var definition = EmailTemplateCatalog.Get(templateKey);
        var html = await ActiveHtmlAsync(definition, cancellationToken);
        return EmailTemplateRenderer.Render(definition, html, values);
    }

    public async Task<EmailSendResult> SendAsync(
        IReadOnlyList<string> recipients,
        string templateKey,
        IReadOnlyDictionary<string, string?> values,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var rendered = await RenderAsync(templateKey, values, cancellationToken);
        return await email.SendAsync(
            new EmailMessage(
                recipients,
                rendered.Subject,
                rendered.Html,
                rendered.Text,
                idempotencyKey,
                EmailTemplateCatalog.Get(templateKey).IsPriority),
            cancellationToken);
    }

    public async Task<IReadOnlyList<AdminEmailTemplateDto>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var overrides = (await store.ListAsync(cancellationToken))
            .ToDictionary(template => template.Key, StringComparer.Ordinal);

        return EmailTemplateCatalog.All
            .Select(definition => ToDto(definition, overrides.GetValueOrDefault(definition.Key)))
            .ToArray();
    }

    public async Task<AdminEmailTemplateDto?> GetAsync(
        string templateKey,
        CancellationToken cancellationToken = default)
    {
        if (!EmailTemplateCatalog.TryGet(templateKey, out var definition))
        {
            return null;
        }

        return ToDto(
            definition!,
            await store.GetAsync(templateKey, cancellationToken));
    }

    public async Task<AdminEmailTemplateDto> UpdateAsync(
        string templateKey,
        string html,
        Guid? updatedByUserId,
        CancellationToken cancellationToken = default)
    {
        var definition = EmailTemplateCatalog.Get(templateKey);
        var errors = EmailTemplateRenderer.Validate(definition, html);
        if (errors.Count > 0)
        {
            throw new EmailTemplateValidationException(errors);
        }

        var saved = await store.SaveAsync(
            definition.Key,
            definition.DisplayName,
            html,
            updatedByUserId,
            cancellationToken);
        return ToDto(definition, saved);
    }

    public Task<AdminEmailTemplatePreviewDto> PreviewAsync(
        string templateKey,
        string html,
        IReadOnlyDictionary<string, string?> values,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(EmailTemplateRenderer.Render(
            EmailTemplateCatalog.Get(templateKey),
            html,
            values));
    }

    public async Task<AdminEmailTemplateDto> ResetAsync(
        string templateKey,
        CancellationToken cancellationToken = default)
    {
        var definition = EmailTemplateCatalog.Get(templateKey);
        await store.DeleteAsync(templateKey, cancellationToken);
        return ToDto(definition, null);
    }

    private async Task<string> ActiveHtmlAsync(
        EmailTemplateDefinition definition,
        CancellationToken cancellationToken)
    {
        var saved = await store.GetAsync(definition.Key, cancellationToken);
        return saved?.Html ?? EmailTemplateCatalog.LoadDefaultHtml(definition);
    }

    private static AdminEmailTemplateDto ToDto(
        EmailTemplateDefinition definition,
        EmailTemplate? saved) =>
        new(
            definition.Key,
            definition.DisplayName,
            saved?.Html ?? EmailTemplateCatalog.LoadDefaultHtml(definition),
            definition.SupportedPlaceholders,
            definition.RequiredPlaceholders,
            saved?.UpdatedAt,
            saved?.UpdatedByUserId,
            saved is not null);
}
