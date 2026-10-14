using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using CompanyHQ.Appointments.Contracts.Dtos;
using CompanyHQ.Appointments.Contracts.Forms;
using CompanyHQ.Appointments.Services;
using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts.Forms;
using CompanyHQ.Auth.Contracts.Identity;

namespace CompanyHQ.Appointments.Controllers;

/// <summary>
/// Appointment diaries: the caller's own, or every diary for a role with <c>appointments.manage</c>.
/// </summary>
[ApiController]
[Route("api/appointments")]
[Authorize]
[Produces("application/json")]
public sealed class AppointmentsController(IAppointmentService appointments) : ControllerBase
{
    /// <summary>
    /// Lists upcoming or past appointments. A manager sees every diary, or one with <c>userId</c>,
    /// in the staff shape.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ClientAppointmentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<ClientAppointmentDto>>> List(
        [FromQuery] PageRequest paging,
        [FromQuery] SearchRequest search,
        bool upcoming = true,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } actingUserId)
        {
            return Unauthorized();
        }

        return await appointments.ListAsync(actingUserId, paging, upcoming, search, userId, cancellationToken);
    }

    /// <summary>Returns one appointment. One out of the caller's reach is a 404, never a 403.</summary>
    [HttpGet("{appointmentId:guid}")]
    [ProducesResponseType<ClientAppointmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientAppointmentDto>> Get(
        Guid appointmentId,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } actingUserId)
        {
            return Unauthorized();
        }

        var result = await appointments.GetAsync(appointmentId, actingUserId, cancellationToken);
        return result is null ? NotFound() : result;
    }

    /// <summary>Adds an appointment to the caller's diary, or to another account's for a manager.</summary>
    [HttpPost]
    [ProducesResponseType<ClientAppointmentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ClientAppointmentDto>> Create(
        CreateAppointmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } actingUserId)
        {
            return Unauthorized();
        }

        try
        {
            var result = await appointments.CreateAsync(actingUserId, request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { appointmentId = result.Id }, result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    /// <summary>Replaces one manually booked appointment.</summary>
    [HttpPut("{appointmentId:guid}")]
    [ProducesResponseType<ClientAppointmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientAppointmentDto>> Update(
        Guid appointmentId,
        UpdateAppointmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } actingUserId)
        {
            return Unauthorized();
        }

        var result = await appointments.UpdateAsync(appointmentId, actingUserId, request, cancellationToken);
        return result is null ? NotFound() : result;
    }

    /// <summary>Removes one manually booked appointment.</summary>
    [HttpDelete("{appointmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid appointmentId,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } actingUserId)
        {
            return Unauthorized();
        }

        return await appointments.DeleteAsync(appointmentId, actingUserId, cancellationToken)
            ? NoContent()
            : NotFound();
    }
}
