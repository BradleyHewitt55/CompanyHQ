using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Services.Auth.Contracts.Forms;

/// <summary>The account holder's own name, typed out to confirm they mean to delete the account.</summary>
public sealed record RequestAccountDeletionRequest([Required, MaxLength(254)] string FullName);
