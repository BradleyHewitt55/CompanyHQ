using CompanyHQ.Media.Contracts.Enums;

namespace CompanyHQ.Media.Contracts.Dtos;

/// <summary>Typed result for lifecycle operations whose HTTP status belongs to the controller.</summary>
public readonly record struct MediaOperationResult(MediaOperationError Error)
{
    public bool Succeeded => Error is MediaOperationError.None;

    public static MediaOperationResult Success() => new(MediaOperationError.None);
}
