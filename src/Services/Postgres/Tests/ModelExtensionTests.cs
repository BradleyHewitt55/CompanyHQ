using System.Text.Json;
using Services.Auth.Models;
using Services.Companies.Contracts.Metadata;
using Services.Postgres.Services;
using Services.Postgres.Providers;
using Services.Sqlite;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Services.Postgres.Tests;

public sealed class ModelExtensionTests : IDisposable
{
    private readonly ServiceProvider provider;
    private readonly CompanyDbContext database;
    private readonly ModelExtensionService service;
    private readonly EntityDefinition user;
    private readonly EntityDefinition order;

    public ModelExtensionTests()
    {
        var services = new ServiceCollection();
        services.AddCompanyData(new SqliteDbProvider(), "Data Source=:memory:");
        provider = services.BuildServiceProvider();
        database = provider.GetRequiredService<CompanyDbContext>();
        database.Database.OpenConnection(); database.Database.EnsureCreated();
        user = new EntityDefinition { Id = Guid.NewGuid(), Name = "User", IsBase = true };
        order = new EntityDefinition { Id = Guid.NewGuid(), Name = "Order" };
        database.AddRange(user, order); database.SaveChanges();
        service = new ModelExtensionService(database);
    }

    [Fact]
    public async Task TypedFieldsRoundTripAndInvalidValuesAreRejected()
    {
        var companyUser = new CompanyUser { Id = Guid.NewGuid(), UserName = "typed@test", DisplayName = "Typed" };
        database.Users.Add(companyUser); await database.SaveChangesAsync();
        var values = new[]
        {
            ("nickname", ExtensionFieldType.String, Json("\"Ace\"")),
            ("score", ExtensionFieldType.Number, Json("42.5")),
            ("active", ExtensionFieldType.Boolean, Json("true")),
            ("joined", ExtensionFieldType.Date, Json("\"2026-07-14\""))
        };
        foreach (var (name, type, value) in values) await service.SetValue(companyUser, await service.AddField(new AddFieldRequest("User", name, type)), value);
        database.ChangeTracker.Clear();
        var loaded = await database.Users.SingleAsync(item => item.Id == companyUser.Id);
        loaded.Extensions.Should().ContainKeys(values.Select(value => value.Item1));
        var invalid = () => ModelExtensionService.Validate(ExtensionFieldType.Number, Json("\"nope\""));
        invalid.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task LinksRejectSelfDuplicatesAndUnknownEntitiesAndBaseColumnsAreSealed()
    {
        var self = () => service.AddLink(new AddLinkRequest("User", "User", LinkCardinality.OneToMany));
        await self.Should().ThrowAsync<InvalidOperationException>();
        await service.AddLink(new AddLinkRequest("User", "Order", LinkCardinality.OneToMany));
        var duplicate = () => service.AddLink(new AddLinkRequest("User", "Order", LinkCardinality.OneToMany));
        await duplicate.Should().ThrowAsync<InvalidOperationException>();
        var unknown = () => service.AddLink(new AddLinkRequest("User", "Missing", LinkCardinality.OneToMany));
        await unknown.Should().ThrowAsync<KeyNotFoundException>();
        var baseColumn = () => service.AddField(new AddFieldRequest("User", "UserName", ExtensionFieldType.String));
        await baseColumn.Should().ThrowAsync<InvalidOperationException>();
    }

    public void Dispose() { database.Dispose(); provider.Dispose(); }
    private static JsonElement Json(string value) => JsonDocument.Parse(value).RootElement.Clone();
}
