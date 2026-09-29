using BusinessWorkflowEngine.Api.Operations;
using BusinessWorkflowEngine.Api.Workflows;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddOpenApi();
var connectionString = builder.Configuration.GetConnectionString("WorkflowDatabase")
    ?? throw new InvalidOperationException("ConnectionStrings:WorkflowDatabase is required.");
builder.Services.AddDbContext<WorkflowDbContext>(options => options.UseNpgsql(connectionString));
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
        options.Authority = authority;
        options.Audience = audience;
        options.RequireHttpsMetadata = true;
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
app.MapWorkflowEndpoints();

app.Run();
