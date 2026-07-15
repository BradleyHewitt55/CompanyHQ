using System.Text.Json;
using Services.Companies.Contracts;

namespace Services.Postgres.Models;

public abstract class BaseModel : IEntityRecord
{
    public Guid Id { get; set; }
    public Dictionary<string, JsonElement> Extensions { get; set; } = [];
}
