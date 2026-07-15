using System.Security.Claims;
using Bunit;
using Services.Auth;
using Services.Auth.Models;
using Services.Auth.Pages;
using Services.Auth.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Web.Admin.Tests;

public sealed class UsersPageTests
{
    [Theory]
    [InlineData(AuthRoles.Owner, true)]
    [InlineData(AuthRoles.Admin, true)]
    [InlineData(AuthRoles.Member, false)]
    public void RoleControlsRenderOfManagementActions(string role, bool expected)
    {
        using var context = new BunitContext();
        context.Services.AddSingleton<IUserDirectoryService>(new FakeDirectory());
        context.Services.AddSingleton<AuthenticationStateProvider>(new FakeAuthenticationStateProvider(role));
        var component = context.Render<UsersPage>();
        component.WaitForState(() => component.FindAll("article").Count == 1);
        component.Markup.Should().Contain("Owner");
        component.FindAll("button").Any().Should().Be(expected);
    }

    private sealed class FakeDirectory : IUserDirectoryService
    {
        public Task<IReadOnlyList<SafeUserDto>> GetUsers(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SafeUserDto>>
            ([new(Guid.NewGuid(), "owner@test", "Owner", "owner@test", [AuthRoles.Owner])]);
    }

    private sealed class FakeAuthenticationStateProvider(string role) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "viewer"), new Claim(ClaimTypes.Role, role)], "test"))));
    }
}
