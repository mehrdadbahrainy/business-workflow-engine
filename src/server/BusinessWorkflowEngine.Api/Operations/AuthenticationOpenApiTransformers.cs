using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace BusinessWorkflowEngine.Api.Operations;

internal static class OpenApiAuthenticationScheme
{
    public static async Task<string?> GetNameAsync(IAuthenticationSchemeProvider schemes)
    {
        var scheme = await schemes.GetDefaultAuthenticateSchemeAsync();
        return scheme?.Name switch
        {
            JwtBearerDefaults.AuthenticationScheme => "Bearer",
            "DevelopmentIdentity" => "DevelopmentUser",
            _ => null
        };
    }
}

internal sealed class AuthenticationOpenApiDocumentTransformer(IAuthenticationSchemeProvider schemes) : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var schemeName = await OpenApiAuthenticationScheme.GetNameAsync(schemes);
        if (schemeName is null) return;

        var scheme = schemeName switch
        {
            "Bearer" => new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Use an access token issued by the configured identity provider."
            },
            "DevelopmentUser" => new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                Name = "X-Demo-User",
                In = ParameterLocation.Header,
                Description = "Development only. Set to the demo subject, for example manager@example.test."
            },
            _ => null
        };
        if (scheme is null) return;

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[schemeName] = scheme;
    }
}

internal sealed class AuthenticationOpenApiOperationTransformer(IAuthenticationSchemeProvider schemes) : IOpenApiOperationTransformer
{
    public async Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var endpointMetadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (!endpointMetadata.OfType<IAuthorizeData>().Any()) return;

        var schemeName = await OpenApiAuthenticationScheme.GetNameAsync(schemes);
        if (schemeName is null || context.Document is null) return;

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(schemeName, context.Document)] = []
        });
        operation.Responses ??= new OpenApiResponses();
        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Authentication is missing or invalid." });
    }
}
