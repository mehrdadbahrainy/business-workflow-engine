using System.Security.Claims;

namespace BusinessWorkflowEngine.Api.Operations;

/// <summary>Public SPA configuration and a server-verified identity endpoint.</summary>
public static class AuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/auth/config", (IConfiguration config, IHostEnvironment environment) =>
        {
            var registrationUrl = config["Authentication:RegistrationUrl"];
            var safeRegistrationUrl = Uri.TryCreate(registrationUrl, UriKind.Absolute, out var url)
                && url.Scheme == Uri.UriSchemeHttps ? url.AbsoluteUri : null;
            return Results.Ok(new
            {
                mode = environment.IsDevelopment() ? "demo" : "oidc",
                authority = environment.IsDevelopment() ? null : config["Authentication:Authority"],
                clientId = environment.IsDevelopment() ? null : config["Authentication:ClientId"],
                scope = config["Authentication:Scope"] ?? "openid profile email",
                registrationUrl = safeRegistrationUrl,
                registrationHint = environment.IsEnvironment("Local")
                    ? "New user? Select Register on the Keycloak login page."
                    : null
            });
        }).AllowAnonymous().WithTags("Authentication").WithName("GetAuthenticationConfig");

        endpoints.MapGet("/api/v1/auth/me", (ClaimsPrincipal user) =>
        {
            var subject = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
            var roleClaimType = user.Identities.FirstOrDefault(identity => identity.IsAuthenticated)?.RoleClaimType ?? ClaimTypes.Role;
            var roles = user.Claims.Where(claim => claim.Type == roleClaimType)
                .Select(claim => claim.Value).Distinct(StringComparer.Ordinal).OrderBy(role => role).ToArray();
            var displayName = user.FindFirstValue("name") ?? user.Identity?.Name ?? subject;
            return Results.Ok(new { subject, displayName, roles });
        }).RequireAuthorization().WithTags("Authentication").WithName("GetAuthenticatedUser");

        return endpoints;
    }
}
