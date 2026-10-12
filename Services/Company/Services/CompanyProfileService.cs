using CompanyHQ.Services.Company.Contracts.Dtos;
using CompanyHQ.Services.Company.Contracts.Forms;
using CompanyHQ.Services.Company.Contracts.Models;
using CompanyHQ.Services.Postgres.Contracts;
using CompanyHQ.Services.Persistence.Services;
using CompanyHQ.Services.Users.Services;

namespace CompanyHQ.Services.Company.Services;

public sealed class CompanyProfileService(
    IRepository<CompanyProfile> profiles,
    IUserAuthorizationService authorization) : ICompanyProfileService
{
    public Task<ClientCompanyProfileDto?> GetAsync(CancellationToken cancellationToken = default) =>
        profiles.Query()
            .Where(profile => profile.Id == CompanyProfile.SingletonId)
            .Select(profile => new ClientCompanyProfileDto(
                profile.Name,
                profile.Website,
                profile.SupportEmail,
                profile.InfoEmail,
                profile.LogoUrl,
                profile.Description,
                profile.UpdatedAt,
                profile.FacebookAccount,
                profile.InstagramAccount,
                profile.WhatsAppNumber,
                profile.PublicEmails))
            .FirstOrDefaultAsync(cancellationToken)!;

    public async Task<ClientCompanyProfileDto> UpdateAsync(
        UpdateCompanyProfileRequest request,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (actingUserId is not { } userId ||
            !await authorization.HasPermissionAsync(userId, AccessPermissions.ManageCompany, cancellationToken))
        {
            throw new UnauthorizedAccessException();
        }

        var name = Clean(request.Name, nameof(request.Name));
        var website = Clean(request.Website, nameof(request.Website));
        var supportEmail = Clean(request.SupportEmail, nameof(request.SupportEmail)).ToLowerInvariant();
        var infoEmail = Clean(request.InfoEmail, nameof(request.InfoEmail)).ToLowerInvariant();
        var logoUrl = CleanOptional(request.LogoUrl);
        var description = Clean(request.Description, nameof(request.Description));
        var publicEmails = NormalizeEmails(request.PublicEmails, supportEmail, infoEmail);

        var existing = await profiles.GetAsync(CompanyProfile.SingletonId, cancellationToken);
        if (existing is null)
        {
            var created = await profiles.AddAsync(
                new CompanyProfile
                {
                    Id = CompanyProfile.SingletonId,
                    Name = name,
                    Website = website,
                    SupportEmail = supportEmail,
                    InfoEmail = infoEmail,
                    PublicEmails = publicEmails,
                    LogoUrl = logoUrl,
                    Description = description,
                    FacebookAccount = CleanOptional(request.FacebookAccount),
                    InstagramAccount = CleanOptional(request.InstagramAccount),
                    WhatsAppNumber = CleanOptional(request.WhatsAppNumber)
                },
                cancellationToken);

            return Dto(created);
        }

        existing.Name = name;
        existing.Website = website;
        existing.SupportEmail = supportEmail;
        existing.InfoEmail = infoEmail;
        existing.PublicEmails = publicEmails;
        existing.LogoUrl = logoUrl;
        existing.Description = description;
        existing.FacebookAccount = CleanOptional(request.FacebookAccount);
        existing.InstagramAccount = CleanOptional(request.InstagramAccount);
        existing.WhatsAppNumber = CleanOptional(request.WhatsAppNumber);

        return Dto(await profiles.UpdateAsync(existing, cancellationToken));
    }

    private static ClientCompanyProfileDto Dto(CompanyProfile profile) =>
        new(
            profile.Name,
            profile.Website,
            profile.SupportEmail,
            profile.InfoEmail,
            profile.LogoUrl,
            profile.Description,
            profile.UpdatedAt,
            profile.FacebookAccount,
            profile.InstagramAccount,
            profile.WhatsAppNumber,
            profile.PublicEmails);

    private static ClientCompanyEmailDto[]? NormalizeEmails(
        IReadOnlyList<ClientCompanyEmailDto>? emails, string supportEmail, string infoEmail)
    {
        if (emails is null) return null;
        if (emails.Count is < 2 or > 20 || emails.Any(email => email is null))
            throw new ArgumentException("Provide between 2 and 20 company email entries.");
        var normalized = emails.Select(email => new ClientCompanyEmailDto(
            Clean(email.Title, nameof(email.Title)),
            Clean(email.Address, nameof(email.Address)).ToLowerInvariant(),
            CleanOptional(email.Description))).ToArray();
        foreach (var email in normalized)
            if (email.Title.Length > 160 || email.Address.Length > 320 || email.Description?.Length > 2_000
                || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email.Address))
                throw new ArgumentException("Each company email needs a valid address, a title of at most 160 characters, and a description of at most 2000 characters.");
        if (normalized[0].Address != supportEmail || normalized[1].Address != infoEmail)
            throw new ArgumentException("The first two public emails must be the support and information addresses.");
        return normalized;
    }

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Clean(string value, string parameterName)
    {
        var cleaned = value?.Trim() ?? string.Empty;

        return cleaned.Length > 0
            ? cleaned
            : throw new ArgumentException("The value cannot be blank.", parameterName);
    }
}
