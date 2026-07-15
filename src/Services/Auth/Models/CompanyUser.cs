using Microsoft.AspNetCore.Identity;
using Services.Companies.Contracts;
using System.Text.Json;

namespace Services.Auth.Models;

public sealed class CompanyUser : IdentityUser<Guid>, IExtensibleEntity
{
    public string DisplayName { get; set; } = string.Empty;
    public Dictionary<string, JsonElement> Extensions { get; set; } = [];
}
