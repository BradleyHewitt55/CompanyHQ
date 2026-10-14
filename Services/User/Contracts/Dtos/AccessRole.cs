namespace CompanyHQ.Users.Contracts.Dtos;

public sealed record AdminAccessRoleDto(Guid Id, string Name, IReadOnlyList<string> Permissions);
