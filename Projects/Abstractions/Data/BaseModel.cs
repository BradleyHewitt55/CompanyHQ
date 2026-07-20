namespace Abstractions.Data;

// Inheriting BaseModel is what marks a model as persistable: every db provider,
// current and future, constrains its generic services on this type.
public abstract class BaseModel
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
}
