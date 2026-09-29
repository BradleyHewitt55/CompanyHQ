namespace CompanyHQ.Services.Media.Contracts.Enums;

/// <summary>Storage-owned classification of an object deletion attempt.</summary>
public enum StorageDeleteOutcome
{
    AlreadyGone,
    Permanent,
    Transient
}
