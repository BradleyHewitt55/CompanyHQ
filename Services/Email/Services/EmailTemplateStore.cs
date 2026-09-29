using Microsoft.EntityFrameworkCore;
using CompanyHQ.Services.Email.Contracts.Models;
using CompanyHQ.Services.Postgres.Services;

namespace CompanyHQ.Services.Email.Services;

internal sealed class EmailTemplateStore(IPostgresService<EmailTemplate> templates) : IEmailTemplateStore
{
    public Task<EmailTemplate?> GetAsync(string key, CancellationToken cancellationToken = default) =>
        templates.Query().SingleOrDefaultAsync(template => template.Key == key, cancellationToken);

    public async Task<IReadOnlyList<EmailTemplate>> ListAsync(CancellationToken cancellationToken = default) =>
        await templates.Query().OrderBy(template => template.Key).ToListAsync(cancellationToken);

    public async Task<EmailTemplate> SaveAsync(
        string key,
        string displayName,
        string html,
        Guid? updatedByUserId,
        CancellationToken cancellationToken = default)
    {
        var existing = await templates.Query().SingleOrDefaultAsync(
            template => template.Key == key,
            cancellationToken);

        if (existing is null)
        {
            return await templates.AddAsync(new EmailTemplate
            {
                Key = key,
                DisplayName = displayName,
                Html = html,
                UpdatedByUserId = updatedByUserId
            }, cancellationToken);
        }

        existing.DisplayName = displayName;
        existing.Html = html;
        existing.UpdatedByUserId = updatedByUserId;
        return await templates.UpdateAsync(existing, cancellationToken);
    }

    public async Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var id = await templates.Query()
            .Where(template => template.Key == key)
            .Select(template => template.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return id != Guid.Empty && await templates.DeleteAsync(id, cancellationToken);
    }
}
