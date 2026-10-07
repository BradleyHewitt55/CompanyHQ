using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CompanyHQ.Services.Blogs.Contracts.Dtos;
using CompanyHQ.Services.Blogs.Contracts.Forms;
using CompanyHQ.Services.Blogs.Services;
using CompanyHQ.Services.Postgres.Contracts.Dtos;
using CompanyHQ.Services.Postgres.Contracts;
using CompanyHQ.Services.Postgres.Contracts.Enums;
using CompanyHQ.Services.Postgres.Contracts.Forms;
using CompanyHQ.Services.Users.Extensions;
using CompanyHQ.Services.Auth.Contracts.Identity;

namespace CompanyHQ.Services.Blogs.Controllers;

/// <summary>Public published blog reads and administrator authoring.</summary>
[ApiController]
[Route("api/blog")]
[Produces("application/json")]
public sealed class BlogController(IBlogService blogs) : ControllerBase
{
    [HttpGet]
    [McpAction(McpCapabilityCatalog.ListBlogPosts, McpCapabilityCatalog.ListBlogPosts,
        "List blog posts. With listing permission, include drafts and audit fields. Match a keyword against one named field, or every text field when none is named; page with page and pageSize.")]
    [AllowAnonymous]
    [ProducesResponseType<PagedResult<ClientBlogPostDto>>(StatusCodes.Status200OK)]
    public Task<PagedResult<ClientBlogPostDto>> List(
        [FromQuery] PageRequest paging,
        [FromQuery] SearchRequest search,
        CancellationToken cancellationToken = default) =>
        blogs.ListAsync(paging, search,
            User.Audience(McpCapabilityCatalog.ListBlogPosts),
            User.GetUserId(), cancellationToken);

    [HttpGet("{slug}")]
    [McpAction(McpCapabilityCatalog.GetBlogPost, McpCapabilityCatalog.GetBlogPost,
        "Get a blog post by stable slug. With the matching permission, drafts and audit fields are included.")]
    [AllowAnonymous]
    [ProducesResponseType<ClientBlogPostDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientBlogPostDto>> Get(
        string slug,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await blogs.GetAsync(slug,
                User.Audience(McpCapabilityCatalog.GetBlogPost),
                User.GetUserId(), cancellationToken) is { } post
                ? post
                : NotFound();
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [McpAction(McpCapabilityCatalog.CreateBlogPost, McpCapabilityCatalog.CreateBlogPost,
        "Create a blog post. Keep it as a draft or set IsDraft false with PublishedAt to publish it.")]
    [Authorize(Policy = McpCapabilityCatalog.CreateBlogPost)]
    [ProducesResponseType<ClientBlogPostDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClientBlogPostDto>> Create(
        CreateBlogPostRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var post = await blogs.CreateAsync(
                request,
                User.GetUserId(),
                cancellationToken);
            return CreatedAtAction(nameof(Get), new { post.Slug }, post);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (BlogSlugConflictException)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Duplicate blog slug.",
                Detail = "A blog post already uses this route slug.",
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid blog post.",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    [HttpPut("{slug}")]
    [McpAction(McpCapabilityCatalog.UpdateBlogPost, McpCapabilityCatalog.UpdateBlogPost,
        "Replace a blog post's editable fields by slug without changing the slug.")]
    [Authorize(Policy = McpCapabilityCatalog.UpdateBlogPost)]
    [ProducesResponseType<ClientBlogPostDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientBlogPostDto>> Update(
        string slug,
        UpdateBlogPostRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await blogs.UpdateAsync(
                slug,
                request,
                User.GetUserId(),
                cancellationToken) is { } post
                ? post
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
                Title = "Invalid blog post.",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    [HttpDelete("{slug}")]
    [McpAction(McpCapabilityCatalog.DeleteBlogPost, McpCapabilityCatalog.DeleteBlogPost,
        "Permanently delete a blog post by its stable slug. A missing slug returns not found.")]
    [Authorize(Policy = McpCapabilityCatalog.DeleteBlogPost)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string slug, CancellationToken cancellationToken = default)
    {
        try
        {
            return await blogs.DeleteAsync(
                slug,
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
