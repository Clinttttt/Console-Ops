using ConsoleOps.Api.Security;
using Microsoft.AspNetCore.Http;

namespace ConsoleOps.Tests.Integration.Api;

public sealed class NetworkExposureTests
{
    [Theory]
    [InlineData("http://localhost:5096")]
    [InlineData("https://LOCALHOST:7250")]
    [InlineData("http://127.0.0.1:5096")]
    [InlineData("http://[::1]:5096")]
    [InlineData("https://localhost:7250;http://localhost:5096")]
    public void IsLoopbackOnly_AcceptsAddressesOnlyThisMachineCanReach(string urls)
    {
        Assert.True(NetworkExposure.IsLoopbackOnly([urls]));
    }

    [Theory]
    [InlineData("http://0.0.0.0:5096")]
    [InlineData("http://*:5096")]
    [InlineData("http://+:5096")]
    [InlineData("http://192.168.1.20:5096")]
    [InlineData("https://console-ops.example.com")]
    [InlineData("http://localhost:5096;http://0.0.0.0:5097")]
    public void IsLoopbackOnly_RejectsAddressesOtherMachinesCanReach(string urls)
    {
        Assert.False(NetworkExposure.IsLoopbackOnly([urls]));
    }

    [Fact]
    public void MustRefuseToStart_WhenReachableFromElsewhereWithoutAKey()
    {
        // The guard that keeps an accountless tool off the network. Asserted on the decision rather than on a
        // booted host, because a guard nobody can test is a guard nobody checks.
        Assert.True(NetworkExposure.MustRefuseToStart(["http://0.0.0.0:5096"], null));
        Assert.True(NetworkExposure.MustRefuseToStart(["https://console-ops.example.com"], "   "));
    }

    [Fact]
    public void MustRefuseToStart_IsSatisfiedByLoopbackOrByAKey()
    {
        // Loopback needs no key, and a key makes a wider binding deliberate rather than accidental.
        Assert.False(NetworkExposure.MustRefuseToStart(["http://localhost:5096"], null));
        Assert.False(NetworkExposure.MustRefuseToStart(["http://0.0.0.0:5096"], "a-configured-key"));
    }

    [Fact]
    public void IsLoopbackOnly_TreatsAnUnparsableAddressAsExposed()
    {
        // Refusing is the safe default when Console Ops cannot tell what it is listening on.
        Assert.False(NetworkExposure.IsLoopbackOnly(["not a url"]));
    }
}

public sealed class ApiKeyAuthenticationTests
{
    [Fact]
    public void RequiresKey_IsSkippedEntirelyWhenNoKeyIsConfigured()
    {
        // Local development stays frictionless: no key configured means no header expected.
        Assert.False(ApiKeyAuthentication.RequiresKey(null, new PathString("/api/projects")));
        Assert.False(ApiKeyAuthentication.RequiresKey("   ", new PathString("/api/projects")));
    }

    [Theory]
    [InlineData("/api/projects", true)]
    [InlineData("/API/projects", true)]
    [InlineData("/api", true)]
    [InlineData("/openapi/v1.json", false)]
    [InlineData("/", false)]
    public void RequiresKey_GuardsTheApiSurfaceOnly(string path, bool expected)
    {
        Assert.Equal(expected, ApiKeyAuthentication.RequiresKey("secret", new PathString(path)));
    }

    [Fact]
    public void IsKeyValid_AcceptsOnlyTheConfiguredKey()
    {
        Assert.True(ApiKeyAuthentication.IsKeyValid("secret", "secret"));
        Assert.False(ApiKeyAuthentication.IsKeyValid("secret", "Secret"));
        Assert.False(ApiKeyAuthentication.IsKeyValid("secret", "secret-with-more"));
        Assert.False(ApiKeyAuthentication.IsKeyValid("secret", string.Empty));
        Assert.False(ApiKeyAuthentication.IsKeyValid("secret", null));
    }

    [Fact]
    public void IsKeyValid_RefusesWhenNoKeyIsConfigured()
    {
        // A blank configured key must never make an arbitrary header valid.
        Assert.False(ApiKeyAuthentication.IsKeyValid(null, "anything"));
        Assert.False(ApiKeyAuthentication.IsKeyValid("  ", "  "));
    }
}
