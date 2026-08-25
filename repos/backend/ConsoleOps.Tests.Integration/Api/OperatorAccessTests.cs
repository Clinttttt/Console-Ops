using ConsoleOps.Api.Security;
using ConsoleOps.Application.Features.Authentication;
using Microsoft.AspNetCore.Http;

namespace ConsoleOps.Tests.Integration.Api;

/// <summary>
/// Who may reach Console Ops, and which requests have to prove it.
/// </summary>
/// <remarks>
/// These are the two decisions that make sign-in safe. The allow list is what stops GitHub authentication from
/// admitting every GitHub account, and the access policy is what keeps the sign-in itself reachable while everything
/// else is closed. Both are pure decisions, so they are asserted directly rather than through a request.
/// </remarks>
public sealed class OperatorAccessTests
{
    [Fact]
    public void AllowList_AdmitsNobodyWhenNothingIsConfigured()
    {
        OperatorAllowList allowList = new([]);

        // Failing closed. An empty list means nobody has said who the operators are, and the alternative reading -
        // everybody - is an ops console anyone can sign in to the moment it is exposed.
        Assert.False(allowList.IsConfigured);
        Assert.False(allowList.Admits("Clinttttt"));
        Assert.False(allowList.Admits(null));
    }

    [Fact]
    public void AllowList_AdmitsOnlyConfiguredLogins()
    {
        OperatorAllowList allowList = new(["Clinttttt", " someone-else "]);

        Assert.True(allowList.Admits("Clinttttt"));
        Assert.True(allowList.Admits("someone-else"));
        Assert.False(allowList.Admits("octocat"));
    }

    [Fact]
    public void AllowList_TreatsALoginAsTheSamePersonWhateverTheCasing()
    {
        OperatorAllowList allowList = new(["Clinttttt"]);

        // GitHub logins are case-insensitive; being locked out by capitalisation would be a bug, not a defence.
        Assert.True(allowList.Admits("clinttttt"));
        Assert.True(allowList.Admits("CLINTTTTT"));
        Assert.True(allowList.Admits("  Clinttttt  "));
    }

    [Fact]
    public void AllowList_IgnoresBlankEntries()
    {
        OperatorAllowList allowList = new(["", "   ", "Clinttttt"]);

        Assert.True(allowList.IsConfigured);
        Assert.False(allowList.Admits(""));
        Assert.False(allowList.Admits("   "));
    }

    [Fact]
    public void Access_RequiresNothingWhenNeitherSignInNorAKeyIsConfigured()
    {
        // Local development is unchanged by the arrival of sign-in: open on loopback, refused elsewhere by the
        // exposure guard.
        Assert.False(ApiAccess.RequiresAuthentication(false, null, "/api/workflows"));
    }

    [Theory]
    [InlineData("/api/workflows")]
    [InlineData("/api/projects")]
    [InlineData("/api/settings/configuration")]
    public void Access_ProtectsTheProductSurfaceOnceSignInIsConfigured(string path)
    {
        Assert.True(ApiAccess.RequiresAuthentication(true, null, path));
    }

    [Theory]
    [InlineData("/api/auth/session")]
    [InlineData("/api/auth/github/start")]
    [InlineData("/api/auth/github/callback")]
    [InlineData("/api/auth/sign-out")]
    public void Access_LeavesTheSignInPathsReachable(string path)
    {
        // Answering the session read with a demand to sign in first would be circular: it is how a screen learns
        // that nobody is signed in.
        Assert.False(ApiAccess.RequiresAuthentication(true, "a-key", path));
        Assert.True(ApiAccess.IsSignInPath(path));
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/swagger")]
    [InlineData("/")]
    public void Access_LeavesEverythingOutsideTheApiAlone(string path)
    {
        // The container's liveness probe answers on /health and has no session to present.
        Assert.False(ApiAccess.RequiresAuthentication(true, "a-key", path));
    }

    [Fact]
    public void Access_IsRequiredByAKeyAloneAsItAlwaysWas()
    {
        Assert.True(ApiAccess.RequiresAuthentication(false, "a-key", "/api/workflows"));
    }

    [Fact]
    public void Exposure_IsSatisfiedByConfiguredSignIn()
    {
        string[] exposed = ["http://0.0.0.0:8080"];

        // Sign-in is the stronger guard of the two, so an exposed deployment does not also need a shared key.
        Assert.True(NetworkExposure.MustRefuseToStart(exposed, apiKey: null, signInConfigured: false));
        Assert.False(NetworkExposure.MustRefuseToStart(exposed, apiKey: null, signInConfigured: true));
    }
}
