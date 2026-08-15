using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ConsoleOps.Application.Integrations.GitHub;
using ConsoleOps.Infrastructure.Integrations.GitHub;

namespace ConsoleOps.Tests.Integration.GitHub;

public sealed class GitHubRepositoryCatalogTests
{
    [Fact]
    public async Task ListRepositoriesAsync_MapsRepositoriesGitHubReports()
    {
        StubHandler handler = new(_ => JsonResponse("""
            [
              {
                "name": "spinner",
                "owner": { "login": "clint" },
                "default_branch": "main",
                "private": true,
                "language": "C#",
                "pushed_at": "2026-08-14T09:00:00Z",
                "html_url": "https://github.com/clint/spinner"
              }
            ]
            """));

        GitHubFactResult<GitHubRepositoryCatalogPage> result =
            await CreateCatalog(handler).ListRepositoriesAsync(null, CancellationToken.None);

        GitHubRepositorySummary repository = Assert.Single(result.Observation!.Repositories);
        Assert.Equal("clint", repository.Owner);
        Assert.Equal("spinner", repository.Name);
        Assert.Equal("main", repository.DefaultBranch);
        Assert.True(repository.IsPrivate);
        Assert.Equal("C#", repository.Language);
        Assert.Equal(
            new DateTimeOffset(2026, 8, 14, 9, 0, 0, TimeSpan.Zero),
            repository.PushedAtUtc);
        Assert.False(result.Observation.HasMore);
    }

    [Fact]
    public async Task ListRepositoriesAsync_RequestsMostRecentlyPushedFirst()
    {
        StubHandler handler = new(_ => JsonResponse("[]"));

        await CreateCatalog(handler).ListRepositoriesAsync(null, CancellationToken.None);

        string query = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("sort=pushed", query, StringComparison.Ordinal);
        Assert.Contains("direction=desc", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListRepositoriesAsync_FiltersOnOwnerAndName()
    {
        StubHandler handler = new(_ => JsonResponse("""
            [
              {
                "name": "spinner",
                "owner": { "login": "clint" },
                "default_branch": "main",
                "private": false
              },
              {
                "name": "stalltrack",
                "owner": { "login": "acme" },
                "default_branch": "main",
                "private": false
              }
            ]
            """));

        GitHubFactResult<GitHubRepositoryCatalogPage> result = await CreateCatalog(handler)
            .ListRepositoriesAsync("STALL", CancellationToken.None);

        GitHubRepositorySummary repository = Assert.Single(result.Observation!.Repositories);
        Assert.Equal("stalltrack", repository.Name);
    }

    [Fact]
    public async Task ListRepositoriesAsync_SkipsRepositoriesMissingRequiredFacts()
    {
        StubHandler handler = new(_ => JsonResponse("""
            [
              { "name": "no-owner", "default_branch": "main", "private": false },
              { "name": "usable", "owner": { "login": "clint" }, "default_branch": "main", "private": false }
            ]
            """));

        GitHubFactResult<GitHubRepositoryCatalogPage> result =
            await CreateCatalog(handler).ListRepositoriesAsync(null, CancellationToken.None);

        Assert.Equal("usable", Assert.Single(result.Observation!.Repositories).Name);
    }

    [Fact]
    public async Task ListRepositoriesAsync_ReportsFurtherPagesFromLinkHeader()
    {
        StubHandler handler = new(_ =>
        {
            HttpResponseMessage response = JsonResponse("[]");
            response.Headers.TryAddWithoutValidation(
                "Link",
                "<https://api.github.test/user/repos?page=2>; rel=\"next\"");
            return response;
        });

        GitHubFactResult<GitHubRepositoryCatalogPage> result =
            await CreateCatalog(handler).ListRepositoriesAsync(null, CancellationToken.None);

        Assert.True(result.Observation!.HasMore);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, GitHubReadFailure.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, GitHubReadFailure.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound, GitHubReadFailure.NotFound)]
    [InlineData(HttpStatusCode.TooManyRequests, GitHubReadFailure.RateLimited)]
    [InlineData(HttpStatusCode.ServiceUnavailable, GitHubReadFailure.Unavailable)]
    public async Task ListRepositoriesAsync_MapsProviderFailures(
        HttpStatusCode status,
        GitHubReadFailure expected)
    {
        StubHandler handler = new(_ => new HttpResponseMessage(status));

        GitHubFactResult<GitHubRepositoryCatalogPage> result =
            await CreateCatalog(handler).ListRepositoriesAsync(null, CancellationToken.None);

        Assert.Null(result.Observation);
        Assert.Equal(expected, result.Failure);
    }

    [Fact]
    public async Task ListWorkflowsAsync_MapsWorkflowsWithTheirLatestRun()
    {
        StubHandler handler = new(request => request.RequestUri!.AbsolutePath switch
        {
            "/repos/clint/spinner/actions/workflows" => JsonResponse("""
                {
                  "workflows": [
                    {
                      "name": "Deploy Production",
                      "path": ".github/workflows/deploy-production.yml",
                      "state": "active"
                    }
                  ]
                }
                """),
            "/repos/clint/spinner/actions/workflows/deploy-production.yml/runs" => JsonResponse("""
                {
                  "workflow_runs": [
                    {
                      "status": "completed",
                      "conclusion": "success",
                      "updated_at": "2026-08-14T08:00:00Z"
                    }
                  ]
                }
                """),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });

        GitHubFactResult<GitHubWorkflowCatalog> result = await CreateCatalog(handler)
            .ListWorkflowsAsync("clint", "spinner", CancellationToken.None);

        GitHubWorkflowSummary workflow = Assert.Single(result.Observation!.Workflows);
        Assert.Equal("Deploy Production", workflow.Name);
        Assert.Equal(".github/workflows/deploy-production.yml", workflow.Path);
        Assert.Equal("deploy-production.yml", workflow.FileName);
        Assert.True(workflow.Active);
        Assert.Equal(GitHubWorkflowRunConclusion.Success, workflow.LatestRunConclusion);
        Assert.Equal(
            new DateTimeOffset(2026, 8, 14, 8, 0, 0, TimeSpan.Zero),
            workflow.LatestRunCompletedAtUtc);
    }

    [Fact]
    public async Task ListWorkflowsAsync_ReportsNeverRunWithoutInventingATime()
    {
        StubHandler handler = new(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/runs", StringComparison.Ordinal)
                ? JsonResponse("""{ "workflow_runs": [] }""")
                : JsonResponse("""
                    {
                      "workflows": [
                        { "name": "CI", "path": ".github/workflows/ci.yml", "state": "active" }
                      ]
                    }
                    """));

        GitHubFactResult<GitHubWorkflowCatalog> result = await CreateCatalog(handler)
            .ListWorkflowsAsync("clint", "spinner", CancellationToken.None);

        GitHubWorkflowSummary workflow = Assert.Single(result.Observation!.Workflows);
        Assert.Equal(GitHubWorkflowRunConclusion.Never, workflow.LatestRunConclusion);
        Assert.Null(workflow.LatestRunCompletedAtUtc);
    }

    [Fact]
    public async Task ListWorkflowsAsync_KeepsWorkflowWhenItsRunCannotBeRead()
    {
        StubHandler handler = new(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/runs", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : JsonResponse("""
                    {
                      "workflows": [
                        {
                          "name": "Deploy",
                          "path": ".github/workflows/deploy.yml",
                          "state": "disabled_manually"
                        }
                      ]
                    }
                    """));

        GitHubFactResult<GitHubWorkflowCatalog> result = await CreateCatalog(handler)
            .ListWorkflowsAsync("clint", "spinner", CancellationToken.None);

        GitHubWorkflowSummary workflow = Assert.Single(result.Observation!.Workflows);
        Assert.False(workflow.Active);
        Assert.Equal(GitHubWorkflowRunConclusion.Unknown, workflow.LatestRunConclusion);
    }

    [Fact]
    public async Task ListWorkflowsAsync_SendsGitHubApiHeaders()
    {
        StubHandler handler = new(_ => JsonResponse("""{ "workflows": [] }"""));

        await CreateCatalog(handler)
            .ListWorkflowsAsync("clint", "spinner", CancellationToken.None);

        HttpRequestMessage request = handler.LastRequest!;
        Assert.Contains(
            "application/vnd.github+json",
            request.Headers.Accept.Select(value => value.MediaType));
        Assert.Equal(GitHubProjectReader.UserAgent, request.Headers.UserAgent.ToString());
        Assert.Equal(
            GitHubProjectReader.ApiVersion,
            request.Headers.GetValues("X-GitHub-Api-Version").Single());
    }

    [Fact]
    public async Task ListWorkflowsAsync_HonoursCancellation()
    {
        StubHandler handler = new(_ => JsonResponse("""{ "workflows": [] }"""));
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateCatalog(handler)
            .ListWorkflowsAsync("clint", "spinner", cancellation.Token));
    }

    [Fact]
    public async Task GetLatestCommitAsync_MapsTheHeadCommitOfTheRequestedBranch()
    {
        StubHandler handler = new(_ => JsonResponse("""
            [
              {
                "sha": "8a17c2f4e1b9d0a6c3f5e2b8d7a4c1f0e9b6d3a2",
                "commit": {
                  "author": { "date": "2026-08-15T04:00:00Z" },
                  "committer": { "date": "2026-08-15T04:05:00Z" }
                }
              }
            ]
            """));

        GitHubFactResult<GitHubLatestCommit> result = await CreateCatalog(handler)
            .GetLatestCommitAsync("clint", "spinner", "release/1.2", CancellationToken.None);

        Assert.Equal("8a17c2f4e1b9d0a6c3f5e2b8d7a4c1f0e9b6d3a2", result.Observation!.CommitSha);
        Assert.Equal("8a17c2f", result.Observation.ShortCommitSha);
        Assert.Equal(
            new DateTimeOffset(2026, 8, 15, 4, 5, 0, TimeSpan.Zero),
            result.Observation.CommittedAtUtc);

        // The branch is passed to GitHub rather than assumed.
        Assert.Contains("sha=release%2F1.2", handler.LastRequest!.RequestUri!.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLatestCommitAsync_RejectsAShaItCannotTrust()
    {
        StubHandler handler = new(_ => JsonResponse("""[ { "sha": "not-a-sha" } ]"""));

        GitHubFactResult<GitHubLatestCommit> result = await CreateCatalog(handler)
            .GetLatestCommitAsync("clint", "spinner", "main", CancellationToken.None);

        Assert.Null(result.Observation);
        Assert.Equal(GitHubReadFailure.InvalidResponse, result.Failure);
    }

    [Fact]
    public async Task GetLatestCommitAsync_ReportsAnEmptyBranchAsUnreadable()
    {
        StubHandler handler = new(_ => JsonResponse("[]"));

        GitHubFactResult<GitHubLatestCommit> result = await CreateCatalog(handler)
            .GetLatestCommitAsync("clint", "spinner", "main", CancellationToken.None);

        Assert.Null(result.Observation);
        Assert.Equal(GitHubReadFailure.InvalidResponse, result.Failure);
    }

    private static GitHubRepositoryCatalog CreateCatalog(HttpMessageHandler handler) =>
        new(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.github.test/"),
            Timeout = TimeSpan.FromSeconds(2)
        });

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            return Task.FromResult(responder(request));
        }
    }
}
