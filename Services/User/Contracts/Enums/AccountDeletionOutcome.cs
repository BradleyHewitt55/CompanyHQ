namespace CompanyHQ.Users.Contracts.Enums;

/// <summary>What an account deletion did, so a controller can pick between 204, 403, 404, and 409.</summary>
public enum AccountDeletionOutcome
{
    Deleted,
    NotFound,
    Blocked,
    NotAuthorized
}
