using System.Net;
using System.Net.Http.Json;
using ConsoleOps.Api.Features.Diagnostics;
using ConsoleOps.Tests.Integration.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ConsoleOps.Tests.Integration.Diagnostics;

/// <summary>
/// Console Ops answering the question it asks every other application.
/// </summary>
/// <remarks>
/// Its own Deployment column read "Not configured" because nothing served a version. That was accurate, and a poor
/// advertisement for a contract it expects others to honour.
/// </remarks>
[Collection(ConsoleOpsApiCollection.Name)]
public sealed class VersionEndpointTests(ConsoleOpsApiFactory factory)
{
    /// <summary>
    /// A version probe arrives with no credential, so the endpoint has to answer without one - even where sign-in
    /// is configured and the rest of the surface is closed.
    /// </summary>
    [Fact]
    public async Task Reports_the_build_without_asking_who_is_calling()
    {
        using WebApplicationFactory<Program> application = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("GitHub:App:ClientId", "id");
            builder.UseSetting("GitHub:App:ClientSecret", "secret");
            builder.UseSetting("Auth:AllowedGitHubLogins:0", "Clinttttt");
        });
        using HttpClient client = application.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/version");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        VersionResponse? version = await response.Content.ReadFromJsonAsync<VersionResponse>();
        Assert.NotNull(version);
        Assert.Equal("Console Ops", version.Application);
        Assert.False(string.IsNullOrWhiteSpace(version.Version));
    }

    /// <summary>
    /// A test build records no source revision, and the field is absent rather than filled with something plausible.
    /// Version comparison is only deterministic on a full commit, so a guess would produce a confident wrong verdict.
    /// </summary>
    [Fact]
    public async Task Reports_no_commit_when_the_build_recorded_none()
    {
        using HttpClient client = factory.CreateClient();

        VersionResponse? version = await client.GetFromJsonAsync<VersionResponse>("/version");

        Assert.NotNull(version);
        Assert.True(
            version.Commit is null || version.Commit.Length == 40,
            $"A commit must be absent or a full 40 characters, not '{version.Commit}'.");
    }
}
