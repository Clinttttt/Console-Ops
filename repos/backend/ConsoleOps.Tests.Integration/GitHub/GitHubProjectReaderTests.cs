using System.Collections.Concurrent;
using System.Net;
using System.Text;
using ConsoleOps.Application.Integrations.GitHub;
using ConsoleOps.Infrastructure.Integrations.GitHub;

namespace ConsoleOps.Tests.Integration.GitHub;

public sealed class GitHubProjectReaderTests
{
    private const string CommitSha = "0123456789abcdef0123456789abcdef01234567";
    private static readonly DateTimeOffset ObservedAt =
        new(2026, 8, 14, 5, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReadAsync_MapsLatestSourceAndConfiguredWorkflow()
    {
        RecordingHandler handler = new(request => request.RequestUri!.AbsolutePath switch
        {
            "/repos/Clinttttt/Console-Ops/commits" => JsonResponse($$"""
                [
                  {
                    "sha": "{{CommitSha}}",
                    "commit": {
                      "author": { "date": "2026-08-14T04:55:00Z" },
                      "committer": { "date": "2026-08-14T05:00:00Z" }
                    }
                  }
                ]
                """),
            "/repos/Clinttttt/Console-Ops/actions/workflows/ci.yml/runs" => JsonResponse($$"""
                {
                  "workflow_runs": [
                    {
                      "name": "CI",
                      "status": "completed",
                      "conclusion": "success",
                      "head_sha": "{{CommitSha}}",
                      "run_started_at": "2026-08-14T05:01:00Z",
                      "updated_at": "2026-08-14T05:03:30Z"
                    }
                  ]
                }
                """),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });
        GitHubProjectReader reader = CreateReader(handler);

        GitHubProjectReadResult result = await reader.ReadAsync(
            new GitHubProjectReference("Clinttttt", "Console-Ops", "main", "ci.yml"),
            [],
            CancellationToken.None);

        Assert.True(result.Source.IsSuccess);
        GitHubSourceObservation source = Assert.IsType<GitHubSourceObservation>(result.Source.Observation);
        Assert.Equal("Clinttttt/Console-Ops", source.Repository);
        Assert.Equal("main", source.DefaultBranch);
        Assert.Equal(CommitSha, source.CommitSha);
        Assert.Equal("0123456", source.ShortCommitSha);
        Assert.Equal(DateTimeOffset.Parse("2026-08-14T05:00:00Z"), source.CommittedAtUtc);
        Assert.Equal(ObservedAt, source.ObservedAtUtc);

        Assert.True(result.Workflow.IsSuccess);
        GitHubWorkflowObservation workflow =
            Assert.IsType<GitHubWorkflowObservation>(result.Workflow.Observation);
        Assert.Equal("ci.yml", workflow.WorkflowFile);
        Assert.Equal("CI", workflow.WorkflowName);
        Assert.Equal(GitHubWorkflowState.Passed, workflow.State);
        Assert.Equal(CommitSha, workflow.CommitSha);
        Assert.Equal(DateTimeOffset.Parse("2026-08-14T05:01:00Z"), workflow.StartedAtUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-08-14T05:03:30Z"), workflow.CompletedAtUtc);
        Assert.Equal(ObservedAt, workflow.ObservedAtUtc);

        CapturedRequest[] requests = handler.Requests.ToArray();
        Assert.Equal(2, requests.Length);
        Assert.All(requests, request =>
        {
            Assert.Equal("application/vnd.github+json", request.Accept);
            Assert.Equal("ConsoleOps/1.0", request.UserAgent);
            Assert.Equal("2026-03-10", request.ApiVersion);
        });
        Assert.Contains(requests, request =>
            request.Uri.PathAndQuery
                == "/repos/Clinttttt/Console-Ops/commits?sha=main&per_page=1");
        Assert.Contains(requests, request =>
            request.Uri.PathAndQuery
                == "/repos/Clinttttt/Console-Ops/actions/workflows/ci.yml/runs?branch=main&per_page=20");
    }

    [Fact]
    public async Task ReadAsync_RecordsRunHistoryFromTheSameWorkflowRequest()
    {
        const string olderSha = "89abcdef0123456789abcdef0123456789abcdef";
        RecordingHandler handler = new(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/commits")
                ? SourceResponse()
                : JsonResponse($$"""
                    {
                      "workflow_runs": [
                        {
                          "id": 4102,
                          "name": "Deploy",
                          "status": "in_progress",
                          "conclusion": null,
                          "run_number": 42,
                          "head_sha": "{{CommitSha}}",
                          "head_branch": "main",
                          "html_url": "https://github.com/owner/repository/actions/runs/4102",
                          "actor": { "login": "ci-bot" },
                          "run_started_at": "2026-08-14T05:01:00Z",
                          "created_at": "2026-08-14T05:00:30Z",
                          "updated_at": "2026-08-14T05:02:00Z"
                        },
                        {
                          "id": 4101,
                          "name": "Deploy",
                          "status": "completed",
                          "conclusion": "success",
                          "run_number": 41,
                          "head_sha": "{{olderSha}}",
                          "head_branch": "main",
                          "html_url": "https://github.com/owner/repository/actions/runs/4101",
                          "actor": { "login": "ci-bot" },
                          "created_at": "2026-08-13T05:00:00Z",
                          "updated_at": "2026-08-13T05:02:30Z"
                        }
                      ]
                    }
                    """));
        GitHubProjectReader reader = CreateReader(handler);

        GitHubProjectReadResult result = await reader.ReadAsync(
            new GitHubProjectReference("owner", "repository", "main", "deploy.yml"),
            [],
            CancellationToken.None);

        Assert.Equal(2, result.WorkflowRuns.Count);

        GitHubWorkflowRun inFlight = result.WorkflowRuns[0];
        Assert.Equal(4102, inFlight.RunId);
        Assert.Equal(42, inFlight.RunNumber);
        Assert.Equal("deploy.yml", inFlight.WorkflowFile);
        Assert.Equal("Deploy", inFlight.WorkflowName);
        Assert.Equal("main", inFlight.Branch);
        Assert.Equal(CommitSha, inFlight.CommitSha);
        Assert.Equal(GitHubWorkflowState.InProgress, inFlight.State);
        Assert.Equal(DateTimeOffset.Parse("2026-08-14T05:01:00Z"), inFlight.StartedAtUtc);
        Assert.Null(inFlight.CompletedAtUtc);
        Assert.Equal("ci-bot", inFlight.TriggeredBy);
        Assert.Equal("https://github.com/owner/repository/actions/runs/4102", inFlight.RunUrl);
        Assert.Equal(ObservedAt, inFlight.ObservedAtUtc);

        GitHubWorkflowRun completed = result.WorkflowRuns[1];
        Assert.Equal(GitHubWorkflowState.Passed, completed.State);
        // No run_started_at, so the created instant stands in rather than leaving the release undated.
        Assert.Equal(DateTimeOffset.Parse("2026-08-13T05:00:00Z"), completed.StartedAtUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-08-13T05:02:30Z"), completed.CompletedAtUtc);

        // The newest run still answers "what is the workflow doing", from the same request.
        Assert.Equal(
            GitHubWorkflowState.InProgress,
            Assert.IsType<GitHubWorkflowObservation>(result.Workflow.Observation).State);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task ReadAsync_DropsRunsThatCannotBeIdentifiedOrTrusted()
    {
        RecordingHandler handler = new(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/commits")
                ? SourceResponse()
                : JsonResponse($$"""
                    {
                      "workflow_runs": [
                        {
                          "id": null,
                          "status": "completed",
                          "conclusion": "success",
                          "head_sha": "{{CommitSha}}"
                        },
                        {
                          "id": 9002,
                          "status": "completed",
                          "conclusion": "success",
                          "head_sha": "abc1234"
                        },
                        {
                          "id": 9003,
                          "status": "completed",
                          "conclusion": "success",
                          "head_sha": "{{CommitSha}}",
                          "html_url": "https://github.evil.example/owner/repository/actions/runs/9003"
                        },
                        {
                          "id": 9004,
                          "status": "completed",
                          "conclusion": "success",
                          "head_sha": "{{CommitSha}}",
                          "html_url": "https://user:secret@github.com/owner/repository/actions/runs/9004"
                        }
                      ]
                    }
                    """));
        GitHubProjectReader reader = CreateReader(handler);

        GitHubProjectReadResult result = await reader.ReadAsync(
            new GitHubProjectReference("owner", "repository", "main", "deploy.yml"),
            [],
            CancellationToken.None);

        // Runs without an id or a full commit SHA cannot be reconciled later, so they are not recorded.
        Assert.Equal(2, result.WorkflowRuns.Count);
        Assert.All(result.WorkflowRuns, run => Assert.Equal(CommitSha, run.CommitSha));

        // A link outside github.com, or one carrying credentials, is dropped rather than rendered.
        Assert.All(result.WorkflowRuns, run => Assert.Null(run.RunUrl));

        // The branch falls back to the branch that was queried when GitHub omits it.
        Assert.All(result.WorkflowRuns, run => Assert.Equal("main", run.Branch));
    }

    [Fact]
    public async Task ReadAsync_WithoutWorkflowConfiguration_RecordsNoReleases()
    {
        RecordingHandler handler = new(_ => SourceResponse());
        GitHubProjectReader reader = CreateReader(handler);

        GitHubProjectReadResult result = await reader.ReadAsync(
            new GitHubProjectReference("owner", "repository", "main", null),
            [],
            CancellationToken.None);

        Assert.Empty(result.WorkflowRuns);
    }

    [Fact]
    public async Task ReadAsync_WithoutWorkflowConfiguration_DoesNotRequestActions()
    {
        RecordingHandler handler = new(_ => SourceResponse());
        GitHubProjectReader reader = CreateReader(handler);

        GitHubProjectReadResult result = await reader.ReadAsync(
            new GitHubProjectReference("owner", "repository", "main", null),
            [],
            CancellationToken.None);

        Assert.True(result.Source.IsSuccess);
        Assert.True(result.Workflow.IsSuccess);
        GitHubWorkflowObservation workflow =
            Assert.IsType<GitHubWorkflowObservation>(result.Workflow.Observation);
        Assert.Equal(GitHubWorkflowState.NotConfigured, workflow.State);
        Assert.Null(workflow.WorkflowFile);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ReadAsync_WhenSourceFails_PreservesSuccessfulWorkflowFact()
    {
        RecordingHandler handler = new(request => request.RequestUri!.AbsolutePath.EndsWith("/commits")
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : WorkflowResponse("in_progress", null));
        GitHubProjectReader reader = CreateReader(handler);

        GitHubProjectReadResult result = await reader.ReadAsync(
            new GitHubProjectReference("owner", "repository", "main", "deploy.yml"),
            [],
            CancellationToken.None);

        Assert.False(result.Source.IsSuccess);
        Assert.Equal(GitHubReadFailure.Unavailable, result.Source.Failure);
        Assert.Null(result.Source.Observation);
        Assert.True(result.Workflow.IsSuccess);
        Assert.Equal(
            GitHubWorkflowState.InProgress,
            Assert.IsType<GitHubWorkflowObservation>(result.Workflow.Observation).State);
    }

    [Fact]
    public async Task ReadAsync_WhenDeployedCommitIsAnAncestor_MapsProvenDistance()
    {
        const string deployedSha = "89abcdef0123456789abcdef0123456789abcdef";
        RecordingHandler handler = new(request => request.RequestUri!.AbsolutePath.Contains("/compare/")
            ? JsonResponse("""{ "status": "ahead", "ahead_by": 3 }""")
            : SourceResponse());
        GitHubProjectReader reader = CreateReader(handler);

        GitHubProjectReadResult result = await reader.ReadAsync(
            new GitHubProjectReference("owner", "repository", "main", null),
            [deployedSha, deployedSha.ToUpperInvariant(), "0123456"],
            CancellationToken.None);

        GitHubCommitComparison comparison = Assert.Single(result.CommitComparisons);
        Assert.Equal(deployedSha, comparison.DeployedCommitSha);
        Assert.Equal(CommitSha, comparison.SourceCommitSha);
        Assert.Equal(GitHubCommitRelation.DeployedIsAncestor, comparison.Relation);
        Assert.Equal(3, comparison.CommitsBehind);
        Assert.Null(comparison.Failure);
        Assert.Equal(ObservedAt, comparison.ObservedAtUtc);
        Assert.Contains(handler.Requests, request => request.Uri.PathAndQuery
            == $"/repos/owner/repository/compare/{deployedSha}...{CommitSha}?per_page=1");
    }

    [Theory]
    [InlineData("queued", null, GitHubWorkflowState.Queued)]
    [InlineData("waiting", null, GitHubWorkflowState.Queued)]
    [InlineData("in_progress", null, GitHubWorkflowState.InProgress)]
    [InlineData("completed", "success", GitHubWorkflowState.Passed)]
    [InlineData("completed", "failure", GitHubWorkflowState.Failed)]
    [InlineData("completed", "timed_out", GitHubWorkflowState.Failed)]
    [InlineData("completed", "cancelled", GitHubWorkflowState.Cancelled)]
    [InlineData("completed", "neutral", GitHubWorkflowState.Unknown)]
    public async Task ReadAsync_MapsSupportedWorkflowStates(
        string status,
        string? conclusion,
        GitHubWorkflowState expectedState)
    {
        RecordingHandler handler = new(request => request.RequestUri!.AbsolutePath.EndsWith("/commits")
            ? SourceResponse()
            : WorkflowResponse(status, conclusion));
        GitHubProjectReader reader = CreateReader(handler);

        GitHubProjectReadResult result = await reader.ReadAsync(
            new GitHubProjectReference("owner", "repository", "main", "ci.yml"),
            [],
            CancellationToken.None);

        Assert.Equal(
            expectedState,
            Assert.IsType<GitHubWorkflowObservation>(result.Workflow.Observation).State);
    }

    [Fact]
    public async Task ReadAsync_WhenRateLimited_ReturnsSafeFailureWithoutReadingProviderBody()
    {
        RecordingHandler handler = new(_ =>
        {
            HttpResponseMessage response = new(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("sensitive provider details")
            };
            response.Headers.Add("X-RateLimit-Remaining", "0");
            return response;
        });
        GitHubProjectReader reader = CreateReader(handler);

        GitHubProjectReadResult result = await reader.ReadAsync(
            new GitHubProjectReference("owner", "repository", "main", null),
            [],
            CancellationToken.None);

        Assert.Equal(GitHubReadFailure.RateLimited, result.Source.Failure);
        Assert.Null(result.Source.Observation);
    }

    [Fact]
    public async Task ReadAsync_WhenPayloadIsMalformed_ReturnsInvalidResponse()
    {
        RecordingHandler handler = new(_ => JsonResponse("{not-json"));
        GitHubProjectReader reader = CreateReader(handler);

        GitHubProjectReadResult result = await reader.ReadAsync(
            new GitHubProjectReference("owner", "repository", "main", null),
            [],
            CancellationToken.None);

        Assert.Equal(GitHubReadFailure.InvalidResponse, result.Source.Failure);
        Assert.Null(result.Source.Observation);
    }

    [Fact]
    public async Task ReadAsync_WhenCallerCancels_PropagatesCancellation()
    {
        RecordingHandler handler = new(_ => SourceResponse());
        GitHubProjectReader reader = CreateReader(handler);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync(
            new GitHubProjectReference("owner", "repository", "main", null),
            [],
            cancellation.Token));
    }

    private static GitHubProjectReader CreateReader(HttpMessageHandler handler)
    {
        HttpClient client = new(handler)
        {
            BaseAddress = new Uri("https://api.github.test/"),
            Timeout = TimeSpan.FromSeconds(2)
        };

        return new GitHubProjectReader(client, new FixedTimeProvider(ObservedAt));
    }

    private static HttpResponseMessage SourceResponse() => JsonResponse($$"""
        [
          {
            "sha": "{{CommitSha}}",
            "commit": {
              "committer": { "date": "2026-08-14T05:00:00Z" }
            }
          }
        ]
        """);

    private static HttpResponseMessage WorkflowResponse(string status, string? conclusion) =>
        JsonResponse($$"""
            {
              "workflow_runs": [
                {
                  "name": "Deploy",
                  "status": "{{status}}",
                  "conclusion": {{(conclusion is null ? "null" : $"\"{conclusion}\"")}},
                  "head_sha": "{{CommitSha}}",
                  "run_started_at": "2026-08-14T05:01:00Z",
                  "updated_at": "2026-08-14T05:03:30Z"
                }
              ]
            }
            """);

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        public ConcurrentQueue<CapturedRequest> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Enqueue(new CapturedRequest(
                request.RequestUri!,
                string.Join(",", request.Headers.Accept.Select(value => value.MediaType)),
                request.Headers.UserAgent.ToString(),
                request.Headers.GetValues("X-GitHub-Api-Version").Single()));

            return Task.FromResult(responder(request));
        }
    }

    private sealed record CapturedRequest(
        Uri Uri,
        string Accept,
        string UserAgent,
        string ApiVersion);
}
