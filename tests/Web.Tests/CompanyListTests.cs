using Bunit;
using Services.Companies.Models;
using Services.Companies.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Web.Admin.Tests;

public sealed class CompanyListTests
{
    [Fact]
    public void CompanyListRendersNameAndAdminLink()
    {
        using var context = new BunitContext();
        context.Services.AddSingleton<ICompanyRegistry>(new FakeRegistry());
        var component = context.Render<global::Services.Companies.Pages.CompaniesPage>();
        component.WaitForState(() => component.Markup.Contains("Acme"));
        component.Find("a.company-card").GetAttribute("href").Should().Be("https://acme.test/admin");
    }

    private sealed class FakeRegistry : ICompanyRegistry
    {
        public Task<IReadOnlyList<RegisteredCompany>> GetCompanies(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<RegisteredCompany>>
            ([new() { Name = "Acme", Slug = "acme", Host = "acme.test", AdminUrl = "https://acme.test/admin", DbProvider = "postgres", DatabaseName = "companyhq", DeploymentStatus = "running" }]);
        public Task<RegisteredCompany?> GetCompany(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<RegisteredCompany?>(null);
        public Task<RegisteredCompany> Register(RegisteredCompany company, CancellationToken cancellationToken = default) => Task.FromResult(company);
        public Task Delete(RegisteredCompany company, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
