using Microsoft.Extensions.Options;

namespace BusinessWorkflowEngine.Api.Workflows;

public sealed class WorkflowIntegrationBackgroundService(IServiceScopeFactory scopeFactory, IHttpClientFactory httpClientFactory, IOptions<WorkflowIntegrationOptions> options, ILogger<WorkflowIntegrationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var runtime = scope.ServiceProvider.GetRequiredService<WorkflowRuntimeService>();
                var processed = await runtime.ProcessNextHttpActionAsync(httpClientFactory.CreateClient("workflow-integrations"), options.Value, stoppingToken);
                if (!processed) await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogError(exception, "Workflow integration worker failed while processing an action.");
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }
}
