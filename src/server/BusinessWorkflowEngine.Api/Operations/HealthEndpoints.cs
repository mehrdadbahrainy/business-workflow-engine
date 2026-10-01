using BusinessWorkflowEngine.Api.Workflows;

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

        endpoints.MapGet("/readyz", CheckReadiness)
            .WithName("GetReadiness")
            .WithTags("Operations")
            .WithSummary("Check API readiness")
            .WithDescription("Checks whether the API can connect to its workflow database. This does not verify schema compatibility.")
            .Produces(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<IResult> CheckReadiness(WorkflowDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.Database.CanConnectAsync(cancellationToken))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Service unavailable",
                detail: "The workflow database is unavailable.");
        }

        return TypedResults.Ok(new { status = "ready" });
    }
}
