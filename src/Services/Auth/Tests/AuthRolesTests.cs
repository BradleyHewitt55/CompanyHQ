using FluentAssertions;

namespace Services.Auth.Tests;

public sealed class AuthRolesTests
{
    [Fact] public void RolesAreOrderedFromLeastToMostPrivileged() => AuthRoles.All.Should().ContainInOrder(AuthRoles.Viewer, AuthRoles.Member, AuthRoles.Admin, AuthRoles.Owner);
}
