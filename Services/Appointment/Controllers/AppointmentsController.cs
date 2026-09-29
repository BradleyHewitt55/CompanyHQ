using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using CompanyHQ.Services.Appointments.Contracts.Dtos;
using CompanyHQ.Services.Appointments.Contracts.Forms;
using CompanyHQ.Services.Appointments.Services;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Auth.Contracts.Identity;

namespace CompanyHQ.Services.Appointments.Controllers;

/// <summary>The signed-in member's own appointment diary.</summary>
[ApiController]
[Route("api/appointments")]
[Authorize]
[Produces("application/json")]
public sealed class AppointmentsController(IAppointmentService appointments) : ControllerBase
{
    /// <summary>Lists the caller's upcoming or past appointments.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ClientAppointmentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<ClientAppointmentDto>>> List(
        [FromQuery] PageRequest paging,
        bool upcoming = true,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } actingUserId)
        {
            return Unauthorized();
        }

        return await appointments.ListAsync(actingUserId, paging, upcoming, cancellationToken);
    }

    /// <summary>Returns one of the caller's appointments. Anyone else's is a 404, never a 403.</summary>
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

    /// <summary>Adds an appointment to the caller's diary.</summary>
    [HttpPost]
    [ProducesResponseType<ClientAppointmentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ClientAppointmentDto>> Create(
        CreateAppointmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (User.GetUserId() is not { } actingUserId)
        {
            return Unauthorized();
        }

        var result = await appointments.CreateAsync(actingUserId, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { appointmentId = result.Id }, result);
    }

    /// <summary>Replaces one of the caller's manually booked appointments.</summary>
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

    /// <summary>Removes one of the caller's manually booked appointments.</summary>
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
