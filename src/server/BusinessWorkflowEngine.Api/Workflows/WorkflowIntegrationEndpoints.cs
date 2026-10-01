using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BusinessWorkflowEngine.Api.Workflows;

public static partial class WorkflowIntegrationEndpoints
{
    public static IEndpointRouteBuilder MapWorkflowIntegrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/v1/integrations").RequireAuthorization();
        api.MapGet("", List).WithName("ListWorkflowIntegrations").WithTags("Integrations");
        api.MapPost("", Create).WithName("CreateWorkflowIntegration").WithTags("Integrations").RequireAuthorization("WorkflowAuthor").Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status409Conflict);
        api.MapPut("/{key}", Update).WithName("UpdateWorkflowIntegration").WithTags("Integrations").RequireAuthorization("WorkflowAuthor").Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound);
        return endpoints;
    }

    private static async Task<IResult> List(WorkflowDbContext db, CancellationToken cancellationToken)
    {
        var integrations = await db.WorkflowIntegrationConnections.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
        return Results.Ok(integrations.Select(ToResponse));
    }

    private static async Task<IResult> Create(IntegrationWriteRequest request, ClaimsPrincipal user, WorkflowDbContext db, WorkflowIntegrationSecretProtector protector, IOptions<WorkflowIntegrationOptions> options, CancellationToken cancellationToken)
    {
        if (!KeyPattern().IsMatch(request.Key ?? string.Empty) || string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 120)
            return Results.BadRequest(new { error = "Provide a lowercase integration key and a name up to 120 characters." });
        if (!TryNormalizeBaseUrl(request.BaseUrl, options.Value, out var normalizedBaseUrl, out var urlError)) return Results.BadRequest(new { error = urlError });
        if (!ValidHeader(request.AuthHeaderName, request.AuthScheme)) return Results.BadRequest(new { error = "Authentication header name or scheme is invalid." });
        if (!string.IsNullOrEmpty(request.Secret) && string.IsNullOrWhiteSpace(request.AuthHeaderName)) return Results.BadRequest(new { error = "A credential header is required when a secret is provided." });
        if (request.Secret?.Length > 4096 || request.Secret?.Any(char.IsControl) == true) return Results.BadRequest(new { error = "Secret must be at most 4096 characters and contain no control characters." });
        if (await db.WorkflowIntegrationConnections.AnyAsync(x => x.Key == request.Key, cancellationToken)) return Results.Conflict(new { error = "Integration key already exists." });
        string? encrypted = null;
        try { if (!string.IsNullOrEmpty(request.Secret)) encrypted = protector.Protect(request.Secret); }
        catch (InvalidOperationException exception) { return Results.Problem(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable); }
        var now = DateTimeOffset.UtcNow;
        var integration = new WorkflowIntegrationConnection
        {
            Key = request.Key!, Name = request.Name.Trim(), BaseUrl = normalizedBaseUrl,
            AuthHeaderName = encrypted is null ? null : request.AuthHeaderName!.Trim(),
            AuthScheme = encrypted is null ? null : request.AuthScheme!.Trim(), ProtectedSecret = encrypted,
            CreatedBy = WorkflowRuntimeService.Subject(user), CreatedAt = now, UpdatedAt = now
        };
        db.WorkflowIntegrationConnections.Add(integration);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/v1/integrations/{integration.Key}", ToResponse(integration));
    }

    private static async Task<IResult> Update(string key, IntegrationUpdateRequest request, WorkflowDbContext db, WorkflowIntegrationSecretProtector protector, CancellationToken cancellationToken)
    {
        var integration = await db.WorkflowIntegrationConnections.SingleOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (integration is null) return Results.NotFound();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 120) return Results.BadRequest(new { error = "Name is required and must be at most 120 characters." });
        if (request.BaseUrl is not null && !string.Equals(request.BaseUrl.TrimEnd('/'), integration.BaseUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            return Results.Conflict(new { error = "Integration base URLs are immutable. Create a new integration key to target another system." });
        if (!ValidHeader(request.AuthHeaderName, request.AuthScheme)) return Results.BadRequest(new { error = "Authentication header name or scheme is invalid." });
        if (request.Secret?.Length > 4096 || request.Secret?.Any(char.IsControl) == true) return Results.BadRequest(new { error = "Secret must be at most 4096 characters and contain no control characters." });
        if (!request.ClearSecret && string.IsNullOrEmpty(request.Secret) && integration.ProtectedSecret is not null && string.IsNullOrWhiteSpace(request.AuthHeaderName))
            return Results.BadRequest(new { error = "To remove the current credential, select Remove stored credential." });
        try
        {
            if (request.ClearSecret || string.IsNullOrWhiteSpace(request.AuthHeaderName)) integration.ProtectedSecret = null;
            else if (!string.IsNullOrEmpty(request.Secret)) integration.ProtectedSecret = protector.Protect(request.Secret);
        }
        catch (InvalidOperationException exception) { return Results.Problem(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable); }
        integration.Name = request.Name.Trim();
        integration.AuthHeaderName = integration.ProtectedSecret is null ? null : request.AuthHeaderName!.Trim();
        integration.AuthScheme = integration.ProtectedSecret is null ? null : request.AuthScheme!.Trim();
        integration.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToResponse(integration));
    }

    private static bool TryNormalizeBaseUrl(string? value, WorkflowIntegrationOptions options, out string normalized, out string error)
    {
        normalized = string.Empty; error = "Base URL must use HTTPS and its hostname must be listed in INTEGRATION_ALLOWED_HOSTS.";
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.Port != 443 || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || !options.GetAllowedHosts().Contains(uri.Host, StringComparer.OrdinalIgnoreCase)) return false;
        normalized = uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal) ? uri.AbsoluteUri : uri.AbsoluteUri + "/";
        error = string.Empty;
        return true;
    }

    private static bool ValidHeader(string? name, string? scheme) =>
        string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(scheme) ||
        !string.IsNullOrWhiteSpace(name) && HeaderPattern().IsMatch(name) &&
        (name.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ? !string.IsNullOrWhiteSpace(scheme) && SchemePattern().IsMatch(scheme) : string.IsNullOrEmpty(scheme));

    private static object ToResponse(WorkflowIntegrationConnection x) => new { x.Key, x.Name, x.BaseUrl, x.AuthHeaderName, x.AuthScheme, hasSecret = x.ProtectedSecret is not null, x.CreatedAt, x.UpdatedAt };

    [GeneratedRegex("^[a-z][a-z0-9-]{0,63}$", RegexOptions.CultureInvariant)] private static partial Regex KeyPattern();
    [GeneratedRegex("^[A-Za-z0-9-]{1,64}$", RegexOptions.CultureInvariant)] private static partial Regex HeaderPattern();
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9._+-]{0,31}$", RegexOptions.CultureInvariant)] private static partial Regex SchemePattern();

    public sealed record IntegrationWriteRequest(string Key, string Name, string BaseUrl, string? AuthHeaderName, string? AuthScheme, string? Secret);
    public sealed record IntegrationUpdateRequest(string Name, string? BaseUrl, string? AuthHeaderName, string? AuthScheme, string? Secret, bool ClearSecret);
}

public sealed class WorkflowIntegrationSecretProtector(IConfiguration configuration)
{
    public string Protect(string value) => Transform(value, encrypt: true);
    public string Unprotect(string value) => Transform(value, encrypt: false);

    private string Transform(string value, bool encrypt)
    {
        var configuredKey = configuration["Integrations:EncryptionKey"];
        byte[] key;
        try { key = Convert.FromBase64String(configuredKey ?? string.Empty); }
        catch (FormatException) { throw new InvalidOperationException("INTEGRATION_ENCRYPTION_KEY must contain a base64 encoded 32-byte key."); }
        if (key.Length != 32) throw new InvalidOperationException("INTEGRATION_ENCRYPTION_KEY must contain a base64 encoded 32-byte key.");
        using var aes = new AesGcm(key, 16);
        if (encrypt)
        {
            var nonce = RandomNumberGenerator.GetBytes(12);
            var plain = System.Text.Encoding.UTF8.GetBytes(value);
            var cipher = new byte[plain.Length]; var tag = new byte[16];
            aes.Encrypt(nonce, plain, cipher, tag);
            var encryptedBytes = new byte[nonce.Length + tag.Length + cipher.Length];
            nonce.CopyTo(encryptedBytes, 0); tag.CopyTo(encryptedBytes, nonce.Length); cipher.CopyTo(encryptedBytes, nonce.Length + tag.Length);
            return Convert.ToBase64String(encryptedBytes);
        }
        var packed = Convert.FromBase64String(value);
        if (packed.Length < 28) throw new CryptographicException("Encrypted integration secret is malformed.");
        var plainText = new byte[packed.Length - 28];
        aes.Decrypt(packed.AsSpan(0, 12), packed.AsSpan(28), packed.AsSpan(12, 16), plainText);
        return System.Text.Encoding.UTF8.GetString(plainText);
    }
}
