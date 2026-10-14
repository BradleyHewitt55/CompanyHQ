namespace CompanyHQ.Users.Contracts.Enums;

/// <summary>Expected outcomes from an administrator account mutation.</summary>
public enum UserAdministrationError
{
    None,
    NotAdministrator,
    UserNotFound,
    EmailTaken,
    InvalidRole
}
