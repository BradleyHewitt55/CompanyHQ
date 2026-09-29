using Microsoft.EntityFrameworkCore;
using CompanyHQ.Services.Company.Contracts.Dtos;
using CompanyHQ.Services.Company.Contracts.Forms;
using CompanyHQ.Services.Company.Contracts.Models;
using CompanyHQ.Services.Postgres.Services;
using CompanyHQ.Services.Users.Services;

namespace CompanyHQ.Services.Company.Services;

public sealed class CompanyProfileService(
    IPostgresService<CompanyProfile> profiles,
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
                profile.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken)!;

    public async Task<ClientCompanyProfileDto> UpdateAsync(
        UpdateCompanyProfileRequest request,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (actingUserId is not { } userId ||
            !await authorization.IsAdministratorAsync(userId, cancellationToken))
        {
            throw new UnauthorizedAccessException();
        }

        var name = Clean(request.Name, nameof(request.Name));
        var website = Clean(request.Website, nameof(request.Website));
        var supportEmail = Clean(request.SupportEmail, nameof(request.SupportEmail)).ToLowerInvariant();
        var infoEmail = Clean(request.InfoEmail, nameof(request.InfoEmail)).ToLowerInvariant();
        var logoUrl = CleanOptional(request.LogoUrl);
        var description = Clean(request.Description, nameof(request.Description));

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
                    LogoUrl = logoUrl,
                    Description = description
                },
                cancellationToken);

            return Dto(created);
        }

        existing.Name = name;
        existing.Website = website;
        existing.SupportEmail = supportEmail;
        existing.InfoEmail = infoEmail;
        existing.LogoUrl = logoUrl;
        existing.Description = description;

        return Dto(await profiles.UpdateAsync(existing, cancellationToken));
    }

    private static ClientCompanyProfileDto Dto(CompanyProfile profile) =>
        new(profile.Name, profile.Website, profile.SupportEmail, profile.InfoEmail, profile.LogoUrl, profile.Description, profile.UpdatedAt);

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
