using System.Text.Json;

namespace Services.Companies.Contracts;

public interface IExtensibleEntity
{
    Dictionary<string, JsonElement> Extensions { get; set; }
}

public interface IEntityRecord : IExtensibleEntity
{
    Guid Id { get; set; }
}
