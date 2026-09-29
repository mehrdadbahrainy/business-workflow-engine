namespace BusinessWorkflowEngine.Api.Operations;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/healthz", () => TypedResults.Ok(new { status = "ok" }))
            .WithName("GetHealth")
            .WithTags("Operations")
            .WithSummary("Check API liveness")
            .WithDescription("Returns HTTP 200 while the API process is running. This liveness endpoint does not verify database readiness.");

        return endpoints;
    }
}
