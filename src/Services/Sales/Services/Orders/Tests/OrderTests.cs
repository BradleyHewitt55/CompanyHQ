using FluentAssertions;
using Services.Orders.Models;
using Services.Postgres.Models;

namespace Services.Orders.Tests;

public sealed class OrderTests
{
    [Fact] public void OrderUsesPostgresBaseModel() => new Order { Slug = "one", Total = 10 }.Should().BeAssignableTo<BaseModel>();
}
