namespace CompanyHQ.Postgres.Contracts.Enums;

/// <summary>
/// Which view of a resource a caller gets back.
/// </summary>
/// <remarks>
/// Lives here rather than in the User service because every service that projects a DTO needs it,
/// and most of them do not otherwise depend on the account record.
/// <para>
/// <see cref="Client"/> is first so it is the <c>default(ResponseAudience)</c>. A projection picked
/// from an unset value must fall to the narrower shape, never the wider one.
/// </para>
/// </remarks>
public enum ResponseAudience
{
    /// <summary>What any caller may see.</summary>
    Client = 0,

    /// <summary>A signed-in caller's view of their own record: the public shape plus its identifier.</summary>
    Owner = 1,

    /// <summary>The operator's view: everything <see cref="Owner"/> exposes, plus more.</summary>
    Admin = 2
}
