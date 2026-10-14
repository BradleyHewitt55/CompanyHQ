using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using CompanyHQ.Contact.Contracts.Dtos;
using CompanyHQ.Contact.Contracts.Forms;
using CompanyHQ.Contact.Services;

namespace CompanyHQ.Contact.Controllers;

/// <summary>The public contact form behind the marketing site.</summary>
/// <remarks>
/// Anonymous and write-only by design. There is no read endpoint and no stored message: a submission
/// becomes one transactional email to the configured company inbox, so this route can never be turned into a way
/// to enumerate who has written in.
/// </remarks>
[ApiController]
[Route("api/contact")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class ContactController(IContactService contact) : ControllerBase
{
    /// <summary>Sends one message to the CompanyHQ team.</summary>
    [HttpPost]
    [EnableRateLimiting(ContactRateLimit.PolicyName)]
    [ProducesResponseType<ClientContactReceiptDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ClientContactReceiptDto>> Submit(
        ContactRequest request,
        CancellationToken cancellationToken = default)
    {
        var receipt = await contact.SubmitAsync(request, cancellationToken);

        return receipt is null
            ? Problem(
                title: "Your message could not be sent.",
                detail: "The mail provider rejected the message. Please try again shortly.",
                statusCode: StatusCodes.Status502BadGateway)
            : Accepted(receipt);
    }
}
