namespace CompanyHQ.Services.Media.Contracts.Enums;

/// <summary>Expected media operation outcomes mapped by the controller.</summary>
public enum MediaOperationError
{
    None,
    NotFound,
    Forbidden,
    Conflict,
    NotPending,
    ObjectMissing,
    MetadataMismatch,
    UploadFailed
}
