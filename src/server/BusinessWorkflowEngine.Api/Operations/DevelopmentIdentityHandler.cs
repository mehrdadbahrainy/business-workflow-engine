using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace BusinessWorkflowEngine.Api.Operations;

public sealed class DevelopmentIdentityHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var subject = Request.Headers["X-Demo-User"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(subject)) return Task.FromResult(AuthenticateResult.NoResult());
        if (subject.Length > 200 || subject.Any(char.IsControl)) return Task.FromResult(AuthenticateResult.Fail("Invalid demo subject."));
        var claims = new List<Claim> { new("sub", subject), new(ClaimTypes.NameIdentifier, subject), new(ClaimTypes.Role, "workflow-admin") };
        if (subject.Equals("manager@example.test", StringComparison.OrdinalIgnoreCase)) claims.Add(new Claim(ClaimTypes.Role, "manager"));
        if (subject.Equals("teamlead@example.test", StringComparison.OrdinalIgnoreCase)) claims.Add(new Claim(ClaimTypes.Role, "team-lead"));
        if (subject.Equals("department@example.test", StringComparison.OrdinalIgnoreCase)) claims.Add(new Claim(ClaimTypes.Role, "department-manager"));
        if (subject.Equals("hr@example.test", StringComparison.OrdinalIgnoreCase)) claims.Add(new Claim(ClaimTypes.Role, "hr"));
        foreach (var role in Request.Headers["X-Demo-Roles"].FirstOrDefault()?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [])
            if (role.Length <= 64 && !role.Any(char.IsControl)) claims.Add(new Claim(ClaimTypes.Role, role));
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
