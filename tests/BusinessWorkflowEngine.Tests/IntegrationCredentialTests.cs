using System.Security.Cryptography;
using BusinessWorkflowEngine.Api.Workflows;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BusinessWorkflowEngine.Tests;

public sealed class IntegrationCredentialTests
{
    private static WorkflowIntegrationSecretProtector CreateProtector(byte[]? key)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Integrations:EncryptionKey"] = key is null ? null : Convert.ToBase64String(key)
            }).Build();
        return new WorkflowIntegrationSecretProtector(configuration);
    }

    [Fact]
    public void IntegrationSecret_IsEncryptedAndRoundTripsWithMatchingKey()
    {
        var key = Enumerable.Range(0, 32).Select(i => (byte)(i + 1)).ToArray();
        var protector = CreateProtector(key);

        var first = protector.Protect("Bearer-test-secret");
        var second = protector.Protect("Bearer-test-secret");

        Assert.NotEqual("Bearer-test-secret", first);
        Assert.NotEqual(first, second); // A unique random nonce is generated each time.
        Assert.Equal("Bearer-test-secret", protector.Unprotect(first));
        Assert.Equal("Bearer-test-secret", protector.Unprotect(second));
    }

    [Fact]
    public void IntegrationSecret_CannotDecryptWithDifferentKey()
    {
        var source = CreateProtector(Enumerable.Repeat((byte)1, 32).ToArray());
        var other = CreateProtector(Enumerable.Repeat((byte)2, 32).ToArray());
        var encrypted = source.Protect("Do not expose");

        Assert.Throws<CryptographicException>(() => other.Unprotect(encrypted));
    }

    [Fact]
    public void IntegrationSecret_RejectsMissingOrMalformedKey()
    {
        Assert.Throws<InvalidOperationException>(() => CreateProtector(null).Protect("abc"));
        Assert.Throws<InvalidOperationException>(() => CreateProtector(new byte[16]).Protect("abc"));
    }
}
