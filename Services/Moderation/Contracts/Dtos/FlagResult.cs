namespace CompanyHQ.Services.Moderation.Contracts.Dtos;

/// <summary>Typed account-flag raise outcome.</summary>
public readonly record struct FlagResult(ClientAccountFlagDto Value, bool AlreadyActive)
{
    public static FlagResult Created(ClientAccountFlagDto value) => new(value, false);

    public static FlagResult Existing(ClientAccountFlagDto value) => new(value, true);
}
