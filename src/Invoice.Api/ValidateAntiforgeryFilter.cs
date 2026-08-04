using Microsoft.AspNetCore.Antiforgery;

namespace Invoice.Api;

public sealed class ValidateAntiforgeryFilter(IAntiforgery antiforgery)
    : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid antiforgery token",
                type: "https://business-invoice-tracker.invalid/problems/invalid-antiforgery-token",
                detail: "Refresh the page and try the request again.");
        }

        return await next(context);
    }
}
