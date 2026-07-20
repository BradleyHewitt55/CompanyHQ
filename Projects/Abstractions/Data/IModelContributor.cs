using Microsoft.EntityFrameworkCore;

namespace Abstractions.Data;

// Plugins own their entities; the Data service owns the DbContext. This is the
// seam that lets a plugin map its tables without referencing the Data project.
public interface IModelContributor
{
    void Configure(ModelBuilder modelBuilder);
}
