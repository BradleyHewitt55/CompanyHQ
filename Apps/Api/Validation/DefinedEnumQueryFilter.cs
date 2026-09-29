using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Api.Validation;

/// <summary>Rejects numeric enum values that are not members of their declared query enum.</summary>
public sealed class DefinedEnumQueryFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (context.ModelState.IsValid
            && context.ActionDescriptor is ControllerActionDescriptor descriptor)
        {
            foreach (var parameter in descriptor.Parameters)
            {
                var enumType = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
                if (parameter.BindingInfo?.BindingSource != BindingSource.Query
                    || !enumType.IsEnum
                    || !context.ActionArguments.TryGetValue(parameter.Name, out var value)
                    || value is not Enum enumValue
                    || Enum.IsDefined(enumType, enumValue))
                {
                    continue;
                }

                context.ModelState.AddModelError(
                    parameter.Name,
                    $"The value '{enumValue}' is not defined for {enumType.Name}.");
            }
        }

        if (!context.ModelState.IsValid)
        {
            context.Result = new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState));
            return;
        }

        await next();
    }
}
