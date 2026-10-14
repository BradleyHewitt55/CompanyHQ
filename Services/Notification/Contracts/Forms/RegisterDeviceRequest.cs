using System.ComponentModel.DataAnnotations;
using CompanyHQ.Notifications.Contracts.Enums;

namespace CompanyHQ.Notifications.Contracts.Forms;

/// <summary>Registers or refreshes one client push token for the authenticated account.</summary>
public sealed record RegisterDeviceRequest(
    [Required, StringLength(4096)] string Token,
    [EnumDataType(typeof(DevicePlatform))] DevicePlatform Platform);
