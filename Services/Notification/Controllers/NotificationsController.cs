using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using CompanyHQ.Notifications.Contracts.Dtos;
using CompanyHQ.Notifications.Contracts.Forms;
using CompanyHQ.Notifications.Services;
using CompanyHQ.Auth.Contracts.Identity;

namespace CompanyHQ.Notifications.Controllers;

/// <summary>Authenticated device registration and notification opt-out routes.</summary>
[ApiController]
[Authorize]
[Route("api/notifications")]
[Produces("application/json")]
public sealed class NotificationsController(
    INotificationAccountService account) : ControllerBase
{
    [HttpPost("devices")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RegisterDevice(
        RegisterDeviceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        await account.RegisterDeviceAsync(userId, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("devices/{token}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RemoveDevice(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(token) || token.Length > 4096)
        {
            return BadRequest("A device token is required and must be at most 4096 characters.");
        }

        await account.RemoveDeviceAsync(userId, token, cancellationToken);
        return NoContent();
    }

    [HttpGet("preferences")]
    [CompanyHQ.Postgres.Contracts.McpAction(
        CompanyHQ.Postgres.Contracts.McpCapabilityCatalog.GetMyNotificationPreferences,
        CompanyHQ.Postgres.Contracts.McpCapabilityCatalog.GetMyNotificationPreferences,
        "Read the authenticated account's notification preferences and opt-out settings. This never reads another account.")]
    [ProducesResponseType<NotificationPreferencesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<NotificationPreferencesDto>> GetPreferences(
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        return await account.GetPreferencesAsync(userId, cancellationToken);
    }

    [HttpPut("preferences")]
    [ProducesResponseType<NotificationPreferencesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<NotificationPreferencesDto>> UpdatePreferences(
        UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        return await account.UpdatePreferencesAsync(userId, request, cancellationToken);
    }
}
