using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TaskManagement.Api.Filters;

// Runs the FluentValidation validator (if there is one) for every action argument
// before the controller method is called. Errors are turned into a 400 by ExceptionHandlingMiddleware.
// (FluentValidation.AspNetCore auto validation is deprecated so I wrote this small filter instead)
public class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
                continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
                continue;

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            if (!result.IsValid)
            {
                throw new ValidationException(result.Errors);
            }
        }

        await next();
    }
}
