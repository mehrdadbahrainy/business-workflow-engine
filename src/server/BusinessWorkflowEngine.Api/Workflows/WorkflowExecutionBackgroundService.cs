namespace BusinessWorkflowEngine.Api.Workflows;

public sealed class WorkflowExecutionBackgroundService(IServiceScopeFactory scopeFactory, ILogger<WorkflowExecutionBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var runtime = scope.ServiceProvider.GetRequiredService<WorkflowRuntimeService>();
                var processed = await runtime.ProcessNextWorkflowAsync(stoppingToken);
                if (!processed) await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogError(exception, "Workflow execution worker failed while advancing an instance.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }
}
