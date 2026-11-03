using FluentValidation;

namespace Scholar.Api.Infrastructure;

/// <summary>
/// Validates every endpoint argument that has a registered FluentValidation validator before the handler runs.
/// Applied once to the root <c>/api</c> group, so new endpoints are validated automatically.
/// </summary>
internal sealed class ValidationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        foreach (var argument in context.Arguments)
        {
            if (argument is null || argument is CancellationToken || argument is HttpContext)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is IValidator validator)
            {
                var result = await validator.ValidateAsync(new ValidationContext<object>(argument),
                    context.HttpContext.RequestAborted);
                if (!result.IsValid)
                {
                    throw new ValidationException(result.Errors);
                }
            }
        }

        return await next(context);
    }
}
