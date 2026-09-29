namespace BusinessWorkflowEngine.Api.Operations;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/healthz", () => TypedResults.Ok(new { status = "ok" }))
            .WithName("GetHealth")
            .WithTags("Operations");

        return endpoints;
    }
}
