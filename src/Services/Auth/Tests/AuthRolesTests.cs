using FluentAssertions;
using Services.Auth.Models;

namespace Services.Auth.Tests;

public sealed class AuthRolesTests
{
    [Fact] public void RolesAreOrderedFromLeastToMostPrivileged() => AuthRoles.All.Should().ContainInOrder(AuthRoles.Viewer, AuthRoles.Member, AuthRoles.Admin, AuthRoles.Owner);
    [Fact] public void RolesResolveFromTheModelsNamespace() => typeof(AuthRoles).Namespace.Should().Be("Services.Auth.Models");
}
