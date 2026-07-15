using Bunit;
using Bunit.TestDoubles;
using Web.Admin.Components;
using Web.Admin.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Services.Companies.Contracts.Services;

namespace Web.Admin.Tests;

public sealed class AdminComponentTests
{
    [Fact]
    public void ViewerCannotInvokeCircuitManagementControls()
    {
        using var context = new BunitContext();
        context.AddAuthorization().SetAuthorized("viewer");
        context.Services.AddSingleton<IServiceCatalog>(new FakeCatalog());
        context.Services.AddSingleton<IServiceScaffolding>(new FakeScaffolding());
        context.Render<ServicesPage>().FindAll("button.service-toggle").Should().BeEmpty();
        context.Render<FileExplorer>().FindAll("button[aria-label^='Add']").Should().BeEmpty();
    }

    [Fact]
    public void AdminLayoutRendersNavigationAndHeader()
    {
        using var context = new BunitContext();
        context.AddAuthorization().SetAuthorized("owner").SetPolicies("ManageUsers");
        context.Services.AddSingleton<IAdminNavigationCatalog>(new EmptyNavigation());
        context.Services.AddSingleton<IServiceCatalog>(new FakeCatalog());
        context.Services.AddSingleton<IServiceScaffolding>(new FakeScaffolding());
        context.Services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var component = context.Render<AdminLayout>(parameters => parameters.Add(x => x.Body, builder => builder.AddContent(0, "Body")));
        component.Markup.Should().Contain("Dashboard").And.Contain("Users").And.Contain("Services").And.Contain("CompanyHQ");
    }

    [Fact]
    public void ExplorerTogglesAndAddDoesNotToggleParent()
    {
        using var context = new BunitContext();
        context.AddAuthorization().SetAuthorized("owner").SetPolicies("ManageUsers");
        context.Services.AddSingleton<IServiceCatalog>(new FakeCatalog());
        context.Services.AddSingleton<IServiceScaffolding>(new FakeScaffolding());
        var component = context.Render<FileExplorer>();
        var admin = component.Find("button.tree-label");
        admin.GetAttribute("aria-expanded").Should().Be("true");
        admin.Click();
        admin.GetAttribute("aria-expanded").Should().Be("false");
        admin.Click();
        component.Find("button[aria-label='Add Admin service']").Click();
        component.Find("button.tree-label").GetAttribute("aria-expanded").Should().Be("true");
        component.Find("[role='dialog']").TextContent.Should().Contain("Blank service");
        component.FindAll("[role='dialog'] button").Single(button => button.TextContent.Contains("Blank service")).Click();
    }

    [Fact]
    public void ServicesFilterByStateAndSearch()
    {
        using var context = new BunitContext();
        context.AddAuthorization().SetAuthorized("owner").SetPolicies("ManageUsers");
        context.Services.AddSingleton<IServiceCatalog>(new FakeCatalog());
        var component = context.Render<ServicesPage>();
        component.FindAll(".service-card").Should().HaveCount(3);
        component.FindAll(".filter-tab").Single(button => button.TextContent == "Enabled").Click();
        component.FindAll(".service-card").Should().HaveCount(2);
        component.Find("input[aria-label='Search services']").Input("analytics");
        component.FindAll(".service-card").Should().ContainSingle().Which.TextContent.Should().Contain("Analytics");
        component.FindAll(".filter-tab").Single(button => button.TextContent == "Disabled").Click();
        component.FindAll(".service-card").Should().BeEmpty();
    }

    [Fact]
    public void ServicesRerenderOnChangeAndUnsubscribeOnDispose()
    {
        var context = new BunitContext();
        context.AddAuthorization().SetAuthorized("owner").SetPolicies("ManageUsers");
        var catalog = new FakeCatalog();
        context.Services.AddSingleton<IServiceCatalog>(catalog);
        var component = context.Render<ServicesPage>();
        catalog.SubscriberCount.Should().Be(1);
        component.Find("button[aria-label='Toggle Orders']").Click();
        component.Find("[data-service='orders']").TextContent.Should().Contain("Disabled");
        context.Dispose();
        catalog.SubscriberCount.Should().Be(0);
    }

    [Fact]
    public void SettingsLinksOnlyRenderForEnabledServicesWithSettingsPages()
    {
        using var context = new BunitContext();
        context.AddAuthorization().SetAuthorized("owner").SetPolicies("ManageUsers");
        context.Services.AddSingleton<IServiceCatalog>(new FakeCatalog());

        var component = context.Render<ServicesPage>();

        component.FindAll("a.service-settings").Should().ContainSingle()
            .Which.GetAttribute("href").Should().Be("/admin/services/orders/settings");
    }

    [Fact]
    public void ChildSettingsLinksOnlyRenderForEnabledServicesWithSettingsPages()
    {
        using var context = new BunitContext();
        context.AddAuthorization().SetAuthorized("owner").SetPolicies("ManageUsers");
        context.Services.AddSingleton<IServiceCatalog>(new FakeCatalog(includeChildren: true));

        var component = context.Render<ServicesPage>();

        component.Find("[data-service='child-settings'] a.service-settings").GetAttribute("href")
            .Should().Be("/admin/services/child-settings/settings");
        component.FindAll("[data-service='child-disabled'] a.service-settings").Should().BeEmpty();
        component.FindAll("[data-service='child-no-settings'] a.service-settings").Should().BeEmpty();
    }

    private sealed class FakeCatalog : IServiceCatalog
    {
        private EventHandler? changed;
        public int SubscriberCount => changed?.GetInvocationList().Length ?? 0;
        public event EventHandler? Changed { add => changed += value; remove => changed -= value; }
        private readonly List<ServiceSummary> items;

        public FakeCatalog(bool includeChildren = false)
        {
            var children = includeChildren
                ? new ServiceSummary[]
                {
                    new("child-settings", "Child settings", "Child", "1", "Test", "c", "purple", true, true),
                    new("child-disabled", "Child disabled", "Child", "1", "Test", "c", "purple", false, true),
                    new("child-no-settings", "Child no settings", "Child", "1", "Test", "c", "purple", true, false)
                }
                : [];
            items =
            [
            new("orders", "Orders", "Orders", "1", "Test", "o", "purple", true, true, Children: children),
            new("inventory", "Inventory", "Stock", "1", "Test", "i", "green", false, true),
            new("analytics", "Analytics", "Reports", "1", "Test", "a", "blue", true, false)
            ];
        }
        public IReadOnlyList<ServiceSummary> GetServices() => items;
        public void SetEnabled(string id, bool enabled)
        {
            var index = items.FindIndex(item => item.Id == id);
            items[index] = items[index] with { Enabled = enabled };
            changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class EmptyNavigation : IAdminNavigationCatalog
    {
        public event EventHandler? Changed { add { } remove { } }
        public IReadOnlyList<AdminNavigationItem> GetItems() => [];
    }

    private sealed class FakeScaffolding : IServiceScaffolding
    {
        public IReadOnlyList<ServiceTemplateInfo> Templates => [new("blank", "Blank service", "Blank")];
        public Task<ScaffoldedService> Create(string name, string parent, CancellationToken cancellationToken = default) => Task.FromResult(new ScaffoldedService(name, true, name, null));
    }
}
