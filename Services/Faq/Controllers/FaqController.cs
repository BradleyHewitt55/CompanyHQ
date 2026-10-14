using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CompanyHQ.Faqs.Contracts.Dtos;
using CompanyHQ.Faqs.Contracts.Forms;
using CompanyHQ.Faqs.Services;
using CompanyHQ.Postgres.Contracts.Dtos;
using CompanyHQ.Postgres.Contracts;
using CompanyHQ.Postgres.Contracts.Enums;
using CompanyHQ.Postgres.Contracts.Forms;
using CompanyHQ.Users.Extensions;
using CompanyHQ.Auth.Contracts.Identity;

namespace CompanyHQ.Faqs.Controllers;

/// <summary>Public published FAQ reads and administrator authoring.</summary>
[ApiController]
[Route("api/faqs")]
[Produces("application/json")]
public sealed class FaqController(IFaqService faqs) : ControllerBase
{
    [HttpGet]
    [McpAction(McpCapabilityCatalog.ListFaqEntries, McpCapabilityCatalog.ListFaqEntries,
        "List FAQ entries, including drafts when this connection has the listing permission. Match a keyword against one named field, or every text field when none is named; page with page and pageSize.")]
    [AllowAnonymous]
    [ProducesResponseType<PagedResult<ClientFaqEntryDto>>(StatusCodes.Status200OK)]
    public Task<PagedResult<ClientFaqEntryDto>> List(
        [FromQuery] PageRequest paging,
        [FromQuery] SearchRequest search,
        CancellationToken cancellationToken = default) =>
        faqs.ListAsync(paging, search,
            User.Audience(McpCapabilityCatalog.ListFaqEntries),
            User.GetUserId(), cancellationToken);

    [HttpGet("{key}")]
    [McpAction(McpCapabilityCatalog.GetFaqEntry, McpCapabilityCatalog.GetFaqEntry,
        "Get one FAQ entry by stable key. With the matching permission, drafts and audit fields are included.")]
    [AllowAnonymous]
    [ProducesResponseType<ClientFaqEntryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientFaqEntryDto>> Get(
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await faqs.GetAsync(key,
                User.Audience(McpCapabilityCatalog.GetFaqEntry),
                User.GetUserId(), cancellationToken) is { } entry
                ? entry
                : NotFound();
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [McpAction(McpCapabilityCatalog.CreateFaqEntry, McpCapabilityCatalog.CreateFaqEntry,
        "Create an FAQ entry. Set IsDraft false with PublishedAt to publish it.")]
    [Authorize(Policy = McpCapabilityCatalog.CreateFaqEntry)]
    [ProducesResponseType<ClientFaqEntryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClientFaqEntryDto>> Create(
        CreateFaqEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entry = await faqs.CreateAsync(
                request,
                User.GetUserId(),
                cancellationToken);
            return CreatedAtAction(nameof(Get), new { entry.Key }, entry);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (FaqKeyConflictException)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Duplicate FAQ key.",
                Detail = "A FAQ entry already uses this route key.",
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid FAQ entry.",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    [HttpPut("{key}")]
    [McpAction(McpCapabilityCatalog.UpdateFaqEntry, McpCapabilityCatalog.UpdateFaqEntry,
        "Replace an FAQ entry's editable fields by key without changing the key.")]
    [Authorize(Policy = McpCapabilityCatalog.UpdateFaqEntry)]
    [ProducesResponseType<ClientFaqEntryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientFaqEntryDto>> Update(
        string key,
        UpdateFaqEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await faqs.UpdateAsync(
                key,
                request,
                User.GetUserId(),
                cancellationToken) is { } entry
                ? entry
                : NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid FAQ entry.",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    [HttpDelete("{key}")]
    [McpAction(McpCapabilityCatalog.DeleteFaqEntry, McpCapabilityCatalog.DeleteFaqEntry,
        "Delete an FAQ entry by its stable key.")]
    [Authorize(Policy = McpCapabilityCatalog.DeleteFaqEntry)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            return await faqs.DeleteAsync(
                key,
                User.GetUserId(),
                cancellationToken)
                ? NoContent()
                : NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
    }
}
