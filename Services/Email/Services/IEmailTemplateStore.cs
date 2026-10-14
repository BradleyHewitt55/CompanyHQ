using CompanyHQ.Email.Contracts.Models;

namespace CompanyHQ.Email.Services;

/// <summary>Stores only administrator overrides. Packaged defaults remain in the Email assembly.</summary>
public interface IEmailTemplateStore
{
    Task<EmailTemplate?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmailTemplate>> ListAsync(CancellationToken cancellationToken = default);

    Task<EmailTemplate> SaveAsync(
        string key,
        string displayName,
        string html,
        Guid? updatedByUserId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);
}
