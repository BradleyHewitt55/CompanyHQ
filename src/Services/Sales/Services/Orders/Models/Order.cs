using Services.Companies.Contracts;

namespace Services.Orders.Models;

public sealed class Order : Services.Postgres.Models.BaseModel
{
    public required string Slug { get; set; }
    public decimal Total { get; set; }
    public Guid? UserId { get; set; }
}
