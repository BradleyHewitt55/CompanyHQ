using Services.Companies.Contracts;
using Services.Postgres;
using Services.Companies.Contracts.Services;
using Services.Postgres.Providers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Services.Orders.Models;

namespace Services.Postgres.Tests;

[Trait("Category", "Docker")]
public sealed class PostgresEntityServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private ServiceProvider provider = null!;

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        var services = new ServiceCollection();
        services.AddCompanyData(new PostgresDbProvider(), postgres.GetConnectionString(), types =>
        {
            types.Add(typeof(TestEntity));
            types.Add(typeof(DynamicallyLoadedOrder));
            types.AddFromAssembly(typeof(Order).Assembly);
        });
        provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<CompanyDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await provider.DisposeAsync();
        await postgres.DisposeAsync();
    }

    [Fact]
    public async Task CrudRoundTripInsertsUpdatesQueriesAndDeletes()
    {
        var service = provider.GetRequiredService<IEntityService<TestEntity>>();
        var entity = await service.Save(new TestEntity { Slug = "first", Name = "Original" });
        entity.Id.Should().NotBeEmpty();
        (await service.GetById(entity.Id))!.Name.Should().Be("Original");
        entity.Name = "Updated";
        await service.Save(entity);
        (await service.GetBy(item => item.Slug == "first")).Should().ContainSingle().Which.Name.Should().Be("Updated");
        (await service.Get()).Should().ContainSingle();
        (await service.Delete(entity.Id)).Should().BeTrue();
        (await service.Get()).Should().BeEmpty();
    }

    [Fact]
    public async Task DynamicallyRegisteredBaseModelPersistsWithoutEntitySpecificContextCode()
    {
        var service = provider.GetRequiredService<IEntityService<DynamicallyLoadedOrder>>();
        var order = await service.Save(new DynamicallyLoadedOrder { Slug = "order-42", Total = 42.50m });
        (await service.GetById(order.Id))!.Total.Should().Be(42.50m);
    }

    [Fact]
    public async Task ModuleEntityAndConfigurationCreateTableAndSecondStartupIsNoOp()
    {
        var service = provider.GetRequiredService<IEntityService<Order>>();
        await service.Save(new Order { Slug = "module-order", Total = 10 });
        (await service.Get()).Should().ContainSingle(order => order.Slug == "module-order");
        var factory = provider.GetRequiredService<IDbContextFactory<CompanyDbContext>>();
        await using var second = await factory.CreateDbContextAsync();
        (await second.Database.EnsureCreatedAsync()).Should().BeFalse();
    }

    public sealed class TestEntity : global::Services.Postgres.Models.BaseModel { public required string Slug { get; set; } public required string Name { get; set; } }
    public sealed class DynamicallyLoadedOrder : global::Services.Postgres.Models.BaseModel { public required string Slug { get; set; } public decimal Total { get; set; } }
}
