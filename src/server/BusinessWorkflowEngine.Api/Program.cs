using BusinessWorkflowEngine.Api.Operations;
using BusinessWorkflowEngine.Api.Workflows;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<AuthenticationOpenApiDocumentTransformer>();
    options.AddOperationTransformer<AuthenticationOpenApiOperationTransformer>();
});
var connectionString = builder.Configuration.GetConnectionString("WorkflowDatabase")
    ?? throw new InvalidOperationException("ConnectionStrings:WorkflowDatabase is required.");
builder.Services.AddDbContext<WorkflowDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<WorkflowRuntimeService>();
builder.Services.AddScoped<WorkflowIntegrationSecretProtector>();
builder.Services.Configure<WorkflowIntegrationOptions>(builder.Configuration.GetSection("Integrations"));
builder.Services.AddHttpClient("workflow-integrations", client => client.Timeout = TimeSpan.FromSeconds(15))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddHostedService<WorkflowIntegrationBackgroundService>();
builder.Services.AddHostedService<WorkflowExecutionBackgroundService>();
var workflowAdminRole = builder.Configuration["Workflow:AdminRole"] ?? "workflow-admin";
builder.Services.AddAuthorization(options => options.AddPolicy("WorkflowAuthor", policy => policy.RequireRole(workflowAdminRole)));
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddAuthentication("DevelopmentIdentity").AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, DevelopmentIdentityHandler>("DevelopmentIdentity", _ => { });
}
else
{
    var authority = builder.Configuration["Authentication:Authority"];
    var audience = builder.Configuration["Authentication:Audience"];
    if (string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(audience))
        throw new InvalidOperationException("Authentication:Authority and Authentication:Audience are required outside Development.");
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.Authority = authority;
        options.Audience = audience;
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters.RoleClaimType = builder.Configuration["Authentication:RoleClaimType"] ?? ClaimTypes.Role;
    });
}
builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthEndpoints();
app.UseAuthentication();
app.UseAuthorization();
await app.InitializeAsync();
app.MapWorkflowDefinitionEndpoints();
app.MapWorkflowIntegrationEndpoints();
app.MapWorkflowInstanceEndpoints();
app.MapWorkflowEndpoints();

app.Run();
