using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Services.Auth.Contracts.Forms;

/// <summary>
/// Body for <c>POST /api/auth/refresh</c> and <c>POST /api/auth/logout</c>.
/// </summary>
public sealed record RefreshRequest([Required] string RefreshToken);
