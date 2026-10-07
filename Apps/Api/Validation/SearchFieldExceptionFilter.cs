using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using CompanyHQ.Services.Postgres.Extensions;

namespace Api.Validation;

/// <summary>Answers 400 with the searchable field names when a list is asked to search a field the caller's role cannot.</summary>
public sealed class SearchFieldExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not SearchFieldException exception)
        {
            return;
        }

        context.ModelState.AddModelError("field", exception.Message);
        context.Result = new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState));
        context.ExceptionHandled = true;
    }
}
