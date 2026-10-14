using System.ComponentModel.DataAnnotations;

using CompanyHQ.Moderation.Contracts.Enums;

namespace CompanyHQ.Moderation.Contracts.Forms;

/// <summary>Human decision for an account flag.</summary>
public sealed record FlagDecisionRequest(
    [EnumDataType(typeof(AccountFlagStatus))] AccountFlagStatus Status,
    [MaxLength(2000)] string? Note = null);
