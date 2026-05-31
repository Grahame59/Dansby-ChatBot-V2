namespace Dansby.Core.Api.Infrastructure;

internal static class ApiKeyEndpointFilter
{
    public static async ValueTask<object?> RequireApiKey(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var configuredKey = http.RequestServices
            .GetRequiredService<IConfiguration>()["DANSBY_API_KEY"];

        if (string.IsNullOrEmpty(configuredKey) ||
            !http.Request.Headers.TryGetValue("X-Api-Key", out var key) ||
            key != configuredKey)
        {
            return Results.Unauthorized();
        }

        return await next(context);
    }
}
