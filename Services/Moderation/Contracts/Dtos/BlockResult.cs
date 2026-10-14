namespace CompanyHQ.Moderation.Contracts.Dtos;

/// <summary>Block creation outcome, kept free of HTTP concerns.</summary>
public readonly record struct BlockResult(ClientUserBlockDto? Value, BlockFailure? Failure)
{
    public bool Succeeded => Failure is null;

    public static BlockResult Ok(ClientUserBlockDto value) => new(value, null);

    public static BlockResult Fail(BlockFailure failure) => new(null, failure);
}

public enum BlockFailure
{
    NotFound,
    Self
}
