using System.Net;
using System.Text;
using ConsoleOps.Application.Integrations.GitHub;
using ConsoleOps.Infrastructure.Integrations.GitHub;

namespace ConsoleOps.Tests.Integration.GitHub;

/// <summary>
/// The Workflows read. These pin what it recovers from a provider payload, and that a state it cannot recognise
/// is reported as unknown rather than rounded to a familiar one.
/// </summary>
public sealed class GitHubWorkflowInventoryTests
{
    private const string WorkflowsPayload = """
        {
          "total_count": 2,
          "workflows": [
            {
              "id": 101,
              "name": "Deploy production",
              "path": ".github/workflows/deploy-production.yml",
              "state": "active"
            },
            {
              "id": 202,
              "name": "Security scan",
              "path": ".github/workflows/security-scan.yml",
              "state": "disabled_manually"
            }
          ]
        }
        """;

    [Fact]
    public async Task ListWorkflowsAsync_ReadsEachWorkflowWithItsLatestRun()
    {
        StubHandler handler = new(request =>
        {
            string path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/actions/workflows", StringComparison.Ordinal))
            {
                return JsonResponse(WorkflowsPayload);
            }

            return path.Contains("/workflows/101/", StringComparison.Ordinal)
                ? JsonResponse("""
                    {
                      "workflow_runs": [
                        {
                          "id": 535,
                          "run_number": 535,
                          "status": "in_progress",
                          "conclusion": null,
                          "head_branch": "master",
                          "head_sha": "2ac8bf0f4c1e9d7a3b5c8e2f1a4d6b9c0e3f7a21",
                          "event": "push",
                          "actor": { "login": "Clinttttt" },
                          "run_started_at": "2026-08-19T06:57:00Z",
                          "updated_at": "2026-08-19T06:58:10Z",
                          "html_url": "https://github.test/run/535"
                        }
                      ]
                    }
                    """)
                : JsonResponse("""{ "workflow_runs": [] }""");
        });

        GitHubFactResult<GitHubWorkflowInventoryPage> result =
            await CreateInventory(handler).ListWorkflowsAsync("clint", "eemo", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Collection(
            result.Observation!.Workflows,
            workflow =>
            {
                Assert.Equal("Deploy production", workflow.Name);
                Assert.Equal(".github/workflows/deploy-production.yml", workflow.Path);
                Assert.True(workflow.Active);
                // Whether it can be dispatched lives in the definition, which this read does not open.
                Assert.Null(workflow.SupportsManualRun);

                GitHubRunSummary run = workflow.LatestRun!;
                Assert.Equal(535, run.Number);
                Assert.Equal(GitHubRunStatus.InProgress, run.Status);
                // Still going, so it has neither an outcome nor an end.
                Assert.Null(run.Conclusion);
                Assert.Null(run.CompletedAtUtc);
                Assert.Equal("push", run.Event);
                Assert.Equal("Clinttttt", run.Actor);
            },
            workflow =>
            {
                // Disabled at the provider, which is a state and not a failure.
                Assert.Equal("Security scan", workflow.Name);
                Assert.False(workflow.Active);
                // No run recorded, which must not be confused with a run that failed.
                Assert.Null(workflow.LatestRun);
            });
    }

    [Fact]
    public async Task ListWorkflowsAsync_ReportsAnUnrecognisedStateAsUnknownRatherThanAsAnOutcome()
    {
        StubHandler handler = new(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/actions/workflows", StringComparison.Ordinal)
                ? JsonResponse("""
                    {
                      "workflows": [
                        { "id": 1, "name": "CI", "path": ".github/workflows/ci.yml", "state": "active" }
                      ]
                    }
                    """)
                : JsonResponse("""
                    {
                      "workflow_runs": [
                        {
                          "id": 9,
                          "run_number": 9,
                          "status": "something_new",
                          "conclusion": "brand_new_outcome",
                          "head_branch": "main",
                          "head_sha": "abc",
                          "event": "push",
                          "run_started_at": "2026-08-19T06:00:00Z",
                          "updated_at": "2026-08-19T06:01:00Z"
                        }
                      ]
                    }
                    """));

        GitHubFactResult<GitHubWorkflowInventoryPage> result =
            await CreateInventory(handler).ListWorkflowsAsync("clint", "eemo", CancellationToken.None);

        GitHubRunSummary run = Assert.Single(result.Observation!.Workflows).LatestRun!;
        Assert.Equal(GitHubRunStatus.Unknown, run.Status);
        Assert.Null(run.Conclusion);
    }

    [Fact]
    public async Task ListWorkflowsAsync_ReportsARejectedTokenRatherThanARepositoryWithNoAutomation()
    {
        StubHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        GitHubFactResult<GitHubWorkflowInventoryPage> result =
            await CreateInventory(handler).ListWorkflowsAsync("clint", "eemo", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(GitHubReadFailure.Unauthorized, result.Failure);
        Assert.Null(result.Observation);
    }

    [Fact]
    public async Task ListWorkflowsAsync_ReportsAnExhaustedRateLimitAsItself()
    {
        StubHandler handler = new(_ =>
        {
            HttpResponseMessage response = new(HttpStatusCode.Forbidden);
            response.Headers.Add("X-RateLimit-Remaining", "0");
            return response;
        });

        GitHubFactResult<GitHubWorkflowInventoryPage> result =
            await CreateInventory(handler).ListWorkflowsAsync("clint", "eemo", CancellationToken.None);

        // A spent limit reported as a rejected credential would send an operator to reissue a working token.
        Assert.Equal(GitHubReadFailure.RateLimited, result.Failure);
    }

    [Fact]
    public async Task ListWorkflowsAsync_KeepsAWorkflowWhoseLatestRunCouldNotBeRead()
    {
        StubHandler handler = new(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/actions/workflows", StringComparison.Ordinal)
                ? JsonResponse("""
                    {
                      "workflows": [
                        { "id": 1, "name": "CI", "path": ".github/workflows/ci.yml", "state": "active" }
                      ]
                    }
                    """)
                : new HttpResponseMessage(HttpStatusCode.InternalServerError));

        GitHubFactResult<GitHubWorkflowInventoryPage> result =
            await CreateInventory(handler).ListWorkflowsAsync("clint", "eemo", CancellationToken.None);

        // The workflow was read; only its run was not. Losing the inventory over one run would report less
        // than Console Ops actually knows.
        Assert.True(result.IsSuccess);
        GitHubWorkflowDefinition workflow = Assert.Single(result.Observation!.Workflows);
        Assert.Equal("CI", workflow.Name);
        Assert.Null(workflow.LatestRun);
    }

    [Fact]
    public async Task ListRunsAsync_ReadsHistoryNewestFirstAndSaysWhenThereIsMore()
    {
        StubHandler handler = new(_ => JsonResponse("""
            {
              "total_count": 91,
              "workflow_runs": [
                {
                  "id": 938,
                  "run_number": 938,
                  "status": "completed",
                  "conclusion": "success",
                  "head_branch": "master",
                  "head_sha": "2ac8bf0f4c1e9d7a3b5c8e2f1a4d6b9c0e3f7a21",
                  "event": "push",
                  "actor": { "login": "Clinttttt" },
                  "run_started_at": "2026-08-19T06:00:00Z",
                  "updated_at": "2026-08-19T06:04:18Z"
                },
                {
                  "id": 936,
                  "run_number": 936,
                  "status": "completed",
                  "conclusion": "failure",
                  "head_branch": "feature/foo",
                  "head_sha": "a82cef1b3d5e7f9a0c2e4b6d8f1a3c5e7b9d0f24",
                  "event": "pull_request",
                  "actor": { "login": "Clinttttt" },
                  "run_started_at": "2026-08-18T09:00:00Z",
                  "updated_at": "2026-08-18T09:01:51Z"
                }
              ]
            }
            """));

        GitHubFactResult<GitHubRunPage> result = await CreateInventory(handler)
            .ListRunsAsync("clint", "eemo", 101, 20, CancellationToken.None);

        Assert.True(result.IsSuccess);
        // The provider reports 91 runs and this page holds 2, so the screen can say this is recent history.
        Assert.True(result.Observation!.HasMore);
        Assert.Collection(
            result.Observation.Runs,
            run =>
            {
                Assert.Equal(938, run.Number);
                Assert.Equal(GitHubRunConclusion.Passed, run.Conclusion);
                Assert.Equal("master", run.Branch);
            },
            run =>
            {
                Assert.Equal(936, run.Number);
                Assert.Equal(GitHubRunConclusion.Failed, run.Conclusion);
                Assert.Equal("feature/foo", run.Branch);
                // The provider's own event, not translated into something friendlier.
                Assert.Equal("pull_request", run.Event);
            });
    }

    [Fact]
    public async Task ListRunsAsync_AsksForNoMoreThanTheAdapterAllows()
    {
        HttpRequestMessage? sent = null;
        StubHandler handler = new(request =>
        {
            sent = request;
            return JsonResponse("""{ "total_count": 0, "workflow_runs": [] }""");
        });

        GitHubFactResult<GitHubRunPage> result = await CreateInventory(handler)
            .ListRunsAsync("clint", "eemo", 101, 5000, CancellationToken.None);

        // A caller's limit is a request, not an instruction: one query string must not decide how much of a
        // shared rate limit a page load spends.
        Assert.Contains("per_page=50", sent!.RequestUri!.Query, StringComparison.Ordinal);
        Assert.Empty(result.Observation!.Runs);
        Assert.False(result.Observation.HasMore);
    }

    [Fact]
    public async Task ListRunJobsAsync_ReadsJobsWithTheirOwnTimings()
    {
        StubHandler handler = new(_ => JsonResponse("""
            {
              "jobs": [
                {
                  "name": "Prepare",
                  "status": "completed",
                  "conclusion": "success",
                  "started_at": "2026-08-19T06:57:00Z",
                  "completed_at": "2026-08-19T06:58:12Z"
                },
                {
                  "name": "Deploy",
                  "status": "in_progress",
                  "conclusion": null,
                  "started_at": "2026-08-19T06:58:12Z",
                  "completed_at": null
                }
              ]
            }
            """));

        GitHubFactResult<GitHubRunJobs> result =
            await CreateInventory(handler).ListRunJobsAsync("clint", "eemo", 535, CancellationToken.None);

        Assert.Collection(
            result.Observation!.Jobs,
            job =>
            {
                Assert.Equal("Prepare", job.Name);
                Assert.Equal(GitHubRunStatus.Completed, job.Status);
                Assert.Equal(GitHubRunConclusion.Passed, job.Conclusion);
            },
            job =>
            {
                Assert.Equal("Deploy", job.Name);
                Assert.Equal(GitHubRunStatus.InProgress, job.Status);
                Assert.Null(job.Conclusion);
                Assert.Null(job.CompletedAtUtc);
            });
    }

    [Fact]
    public async Task ListRunJobsAsync_NamesTheStepThatFailedRatherThanOnlyTheJob()
    {
        StubHandler handler = new(_ => JsonResponse("""
            {
              "jobs": [
                {
                  "name": "Backend",
                  "status": "completed",
                  "conclusion": "failure",
                  "started_at": "2026-08-19T06:00:00Z",
                  "completed_at": "2026-08-19T06:02:41Z",
                  "steps": [
                    {
                      "name": "Checkout",
                      "number": 1,
                      "status": "completed",
                      "conclusion": "success",
                      "started_at": "2026-08-19T06:00:00Z",
                      "completed_at": "2026-08-19T06:00:04Z"
                    },
                    {
                      "name": "Integration tests",
                      "number": 2,
                      "status": "completed",
                      "conclusion": "failure",
                      "started_at": "2026-08-19T06:00:04Z",
                      "completed_at": "2026-08-19T06:02:35Z"
                    },
                    {
                      "name": "Publish results",
                      "number": 3,
                      "status": "completed",
                      "conclusion": "skipped",
                      "started_at": null,
                      "completed_at": null
                    }
                  ]
                }
              ]
            }
            """));

        GitHubFactResult<GitHubRunJobs> result =
            await CreateInventory(handler).ListRunJobsAsync("clint", "eemo", 535, CancellationToken.None);

        GitHubRunJob job = Assert.Single(result.Observation!.Jobs);
        Assert.Equal(3, job.Steps.Count);
        Assert.Equal("Integration tests", job.Steps[1].Name);
        Assert.Equal(GitHubRunConclusion.Failed, job.Steps[1].Conclusion);
        // Skipped because the failure cascaded, which is a different outcome from failing.
        Assert.Equal(GitHubRunConclusion.Skipped, job.Steps[2].Conclusion);
        Assert.Null(job.Steps[2].StartedAtUtc);
    }

    [Fact]
    public async Task ListRunJobsAsync_ReportsNoStepsForAJobThatHasNotStarted()
    {
        StubHandler handler = new(_ => JsonResponse("""
            {
              "jobs": [
                { "name": "Deploy", "status": "queued", "conclusion": null, "steps": [] }
              ]
            }
            """));

        GitHubFactResult<GitHubRunJobs> result =
            await CreateInventory(handler).ListRunJobsAsync("clint", "eemo", 535, CancellationToken.None);

        GitHubRunJob job = Assert.Single(result.Observation!.Jobs);
        Assert.Empty(job.Steps);
        Assert.Equal(GitHubRunStatus.Queued, job.Status);
    }

    private static GitHubWorkflowInventory CreateInventory(HttpMessageHandler handler) =>
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
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(responder(request));
        }
    }
}
