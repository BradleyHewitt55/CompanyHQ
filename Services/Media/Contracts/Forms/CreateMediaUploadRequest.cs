using System.ComponentModel.DataAnnotations;

using CompanyHQ.Services.Media.Contracts.Enums;

namespace CompanyHQ.Services.Media.Contracts.Forms;

/// <summary>Client-declared metadata used to constrain the upload ticket before storage verifies it.</summary>
public sealed record CreateMediaUploadRequest(
    [Required] MediaScope? Scope,
    [Required, MaxLength(128)] string ContentType,
    [Range(1L, long.MaxValue)] long SizeBytes);
