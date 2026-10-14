using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CompanyHQ.Email.Contracts.Dtos;
using CompanyHQ.Email.Contracts.Forms;
using CompanyHQ.Email.Services;
using CompanyHQ.Postgres.Contracts;
using CompanyHQ.Auth.Contracts.Identity;

namespace CompanyHQ.Email.Controllers;

[ApiController]
[Route("api/email-templates")]
[Produces("application/json")]
public sealed class EmailTemplatesController(IEmailTemplateService templates) : ControllerBase
{
    [HttpGet]
    [McpAction(McpCapabilityCatalog.ListEmailTemplates, McpCapabilityCatalog.ListEmailTemplates,
        "List the controlled email templates and their current HTML. Use this before choosing a key to inspect or edit.")]
    [Authorize(Policy = McpCapabilityCatalog.ListEmailTemplates)]
    [ProducesResponseType<IReadOnlyList<AdminEmailTemplateDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<AdminEmailTemplateDto>> List(CancellationToken cancellationToken = default) =>
        templates.ListAsync(cancellationToken);

    [HttpGet("{key}")]
    [McpAction(McpCapabilityCatalog.GetEmailTemplate, McpCapabilityCatalog.GetEmailTemplate,
        "Get one controlled email template by its key, including the current HTML and placeholders.")]
    [Authorize(Policy = McpCapabilityCatalog.GetEmailTemplate)]
    [ProducesResponseType<AdminEmailTemplateDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminEmailTemplateDto>> Get(
        string key,
        CancellationToken cancellationToken = default)
    {
        var template = await templates.GetAsync(key, cancellationToken);
        return template is null ? NotFound() : template;
    }

    [HttpPut("{key}")]
    [McpAction(McpCapabilityCatalog.UpdateEmailTemplate, McpCapabilityCatalog.UpdateEmailTemplate,
        "Replace a controlled email template's HTML after placeholder validation. Use preview first to inspect rendered output.")]
    [Authorize(Policy = McpCapabilityCatalog.UpdateEmailTemplate)]
    [ProducesResponseType<AdminEmailTemplateDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminEmailTemplateDto>> Update(
        string key,
        UpdateEmailTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await templates.UpdateAsync(key, request.Html, User.GetUserId(), cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (EmailTemplateValidationException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid email template.",
                Detail = string.Join(" ", exception.Errors),
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    [HttpPost("{key}/preview")]
    [McpAction(McpCapabilityCatalog.PreviewEmailTemplate, McpCapabilityCatalog.PreviewEmailTemplate,
        "Render supplied template HTML and placeholder values without sending email or saving changes.")]
    [Authorize(Policy = McpCapabilityCatalog.PreviewEmailTemplate)]
    [ProducesResponseType<AdminEmailTemplatePreviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminEmailTemplatePreviewDto>> Preview(
        string key,
        PreviewEmailTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await templates.PreviewAsync(
                key,
                request.Html,
                request.Values?.ToDictionary(pair => pair.Key, pair => (string?)pair.Value)
                    ?? new Dictionary<string, string?>(),
                cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (EmailTemplateValidationException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid email template.",
                Detail = string.Join(" ", exception.Errors),
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    [HttpPost("{key}/reset")]
    [McpAction(McpCapabilityCatalog.ResetEmailTemplate, McpCapabilityCatalog.ResetEmailTemplate,
        "Remove the custom version of a controlled email template and restore its built-in default.")]
    [Authorize(Policy = McpCapabilityCatalog.ResetEmailTemplate)]
    [ProducesResponseType<AdminEmailTemplateDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminEmailTemplateDto>> Reset(
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await templates.ResetAsync(key, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
