using Abstractions.Data;
using Microsoft.EntityFrameworkCore;

namespace Abstractions.Users;

[Index(nameof(Email), IsUnique = true)]
public sealed class User : BaseModel
{
    public required string Name { get; set; }
    public required string Surname { get; set; }
    public required string Email { get; set; }
    public required string ContactNumber { get; set; }
    public required string Role { get; set; }
    public required string PasswordHash { get; set; }
}
