# Console Ops V1 Backend and API Contract

## Status

Approved during Phase 0 on 2026-08-14. This document freezes V1 backend behavior before database
migrations and provider adapters are implemented.

Authority remains:

1. `Console_Ops_Project_Context.md` for product behavior and scope.
2. `Console_Ops_Architecture.md` for implementation boundaries.
3. This document for V1 transport semantics and integration decisions.

The Angular contract in
`repos/frontend/console-ops-web/src/app/core/contracts/dashboard-overview.ts` mirrors the dashboard
portion of this document. If they disagree, correct both in the same change before backend or
frontend implementation continues.

## V1 boundary

V1 supports:

- project and environment registration;
- GitHub source state and one explicitly configured GitHub Actions workflow;
- application health and version probes;
- deterministic source-to-deployed version comparison;
- stored observations and transition-based activity;
- the read-only Overview query;
- release history recorded from GitHub Actions workflow runs.

V1 does not claim Azure revision state, restart/migration/container events, configuration
completeness, logs, rollback, uptime percentages, or Docker state. Those facts must
be omitted or reported as unavailable until their product phase is implemented.

Deployment history was added on 2026-08-15, limited to what GitHub Actions proves: workflow runs and
their outcomes, reconciled against runtime version observations. Runtime revisions remain absent.

## Exposure and the optional API key

Console Ops has no user accounts by design: it is a single-operator tool, and the product context defers
multi-user concerns. That is safe while the API answers only on loopback, which is how it is configured
to run.

Two controls keep that assumption from decaying silently:

- **Startup guard.** If the API is bound to any non-loopback address - a wildcard, `0.0.0.0`, or a real
  interface address - and `Api:Key` is not configured, it refuses to start and names the setting to add.
  An address it cannot parse counts as exposed. Accidental exposure therefore fails loudly instead of
  quietly serving repository names to the network.
- **Optional shared key.** When `Api:Key` is configured, every `/api` request must send
  `X-Console-Ops-Key`, compared in fixed time. When it is not configured the check is skipped entirely,
  so local development needs no header.

The key says "this caller knows the shared secret" and nothing about who they are. It is a guard for one
operator's own API, not an authorization system, and it is not a substitute for real authentication if
Console Ops ever serves more than one person.

No CORS policy is registered, so a browser on another origin cannot call the API; the Angular dev server
proxies instead.

## GitHub discovery API

Added 2026-08-14 for the Add Project import flow. Provider reads, not stored entities; see
`Console_Ops_Add_Project_Import_Plan.md` for the phases and the trust rules.

```text
GET /api/github/repositories?query=
GET /api/github/repositories/{owner}/{repository}/workflows
GET /api/github/repositories/{owner}/{repository}/commits/latest?branch=
GET /api/github/repositories/{owner}/{repository}/endpoints?branch=
```

`GET /api/github/repositories` returns `{ repositories: [...], hasMore }`, where a repository carries
`owner`, `name`, `defaultBranch`, `isPrivate`, `language`, `pushedAt`, and `htmlUrl`. Results are the
repositories the configured credential can see, sorted by push time, filtered on owner and name, and
capped; `hasMore` is true when GitHub reported further pages or the cap truncated the list.

`GET .../workflows` returns `{ workflows: [...] }`, where a workflow carries `name`, `path`, `fileName`,
`active`, `latestRunConclusion`, and `latestRunCompletedAt`. `fileName` is separate from `path` because
the project resource stores the file name while the operator needs the path to recognise the workflow.
`latestRunConclusion` is `success`, `failure`, `cancelled`, `inProgress`, `unknown`, or `never`.

Failures use stable codes: `GitHub.Unauthorized`, `GitHub.RateLimited`, `GitHub.NotFound`,
`GitHub.InvalidResponse`, `GitHub.Unavailable`. A rejected or missing credential is a server
configuration fault, so it is a failure rather than invalid input. No description includes the
credential, the target URL, or a raw provider payload.

Discovery never selects on the operator's behalf. The API returns what exists; the operator confirms
which workflow deploys the environment, as the workflow rules below require.

`GET .../commits/latest` returns the head commit of the requested branch as `commitSha`,
`commitShortSha`, and `committedAt`. It is read for one chosen repository, never per row of a list. A
branch with no commits, or a SHA that is not full hexadecimal, is reported as unreadable rather than
passed through.

That commit is what lets a setup screen compare source with a deployed commit before registration.
Equal normalized SHAs are `In Sync`. Unequal SHAs may only be reported as differing: `Behind` requires
ancestry, which is established by a project refresh, not by comparing two strings.

`GET .../endpoints` returns `{ endpoints: [...], inspectedFileCount }`, where each entry carries `kind`
(`health` or `version`), `path`, and the `sourceFile` it was read from. These are **detections, not
configuration**: the operator confirms each one, and only a probe proves an endpoint answers.

The detection is deliberately narrow, because a wrong suggestion is worse than none:

- It inspects at most five `Program.cs` or `Startup.cs` files, each capped in size, found through one
  repository tree read. No cloning, no code search, no crawling.
- It accepts only string literals registered on the application builder. A route registered on a
  `MapGroup` variable carries a prefix the reader cannot see, and a path read from configuration is not
  a literal; both yield nothing rather than a path at the wrong address.
- `inspectedFileCount` lets a caller distinguish "nothing found" from "nothing read".

## Transport rules

- Use JSON with camel-case property names.
- Use ISO-8601 UTC strings for instants and `DateTimeOffset` in .NET.
- Use `null` when a fact could not be established. Do not use zero, an empty SHA, or the current time
  as a substitute.
- Use stable machine-readable enums/codes for decisions and concise labels only in dashboard read
  models.
- Return RFC 7807 Problem Details for failures and Validation Problem Details for invalid input.
- Never serialize EF entities, provider SDK models, raw provider errors, secrets, or credentialed
  URLs.
- Pass a cancellation token through every request, database call, and outbound request.

## Planned project API

```text
POST   /api/projects
PUT    /api/projects/{projectId}
GET    /api/projects/{projectId}
GET    /api/projects
DELETE /api/projects/{projectId}                 soft archive
POST   /api/projects/{projectId}/refresh          safe observation refresh
GET    /api/dashboard/overview
```

Project registration owns configuration, not credentials.

```text
Project
|-- id
|-- name
|-- description (optional)
|-- configurationVersion
|-- createdAtUtc
|-- updatedAtUtc (optional)
|-- repository
|   |-- owner
|   |-- name
|   |-- defaultBranch
|   `-- workflowFile (optional)
`-- environments[]
    |-- id
    |-- name
    |-- kind: production | staging | development | local
    |-- applicationUrl (optional)
    |-- healthUrl (optional)
    `-- versionUrl (optional)
```

Repository owner/name/default branch are required. `workflowFile` is an explicit GitHub Actions
workflow file name such as `ci.yml`; if absent, CI is `notConfigured`. Console Ops must not select an
arbitrary latest workflow.

Every project must have at least one environment. Environment names must be unique inside a project.
URLs must be absolute HTTP(S) URLs without embedded credentials. The probe implementation will add
explicit scheme, redirect, timeout, response-size, and outbound-address safeguards before making a
request.

`PUT` replaces the editable project configuration. It must send the `configurationVersion` returned
by the latest register/get/list/update response. Existing environments send their current `id`; a
new environment omits `id`. Unknown environment IDs and stale configuration versions return a
conflict rather than silently overwriting newer state.

`DELETE` archives a project in V1. Normal get/list/dashboard queries exclude archived projects.
Archiving preserves history but releases the active name and repository uniqueness constraints so
the same project can be registered again intentionally.

## GitHub source and workflow facts

GitHub is the source of truth for source and CI facts.

Source observation contains:

- repository (`owner/name`);
- default branch;
- full and short commit SHA;
- commit time when GitHub provides it;
- observation time.

Workflow observation contains:

- provider (`githubActions`);
- configured workflow file/name when known;
- state: `queued`, `inProgress`, `passed`, `failed`, `cancelled`, `unknown`, or `notConfigured`;
- associated full commit SHA when known;
- start/completion times when known;
- observation time.

GitHub credentials come from user-secrets, environment variables, managed identity-compatible
configuration, or a production secret store. Tokens are never stored on a `Project` or returned by
an endpoint.

## Health probe contract

The health URL is optional. If absent, health is `notConfigured`.

- A successful 2xx response with no supported body is `healthy` because the configured health
  endpoint responded successfully.
- If a supported health payload reports `Healthy`, `Degraded`, or `Unhealthy`, preserve that state.
- A reachable non-success response is `unhealthy` unless a supported body gives a more precise safe
  status.
- Timeout, DNS, connection, TLS, or blocked-target failures are `unreachable`.
- Record response duration and observation time for every completed attempt.
- Dependency rows are populated only from a supported safe health payload. Console Ops never guesses
  that PostgreSQL, Redis, or another dependency exists.
- Do not store or return unrestricted response bodies.

## Version probe contract

The version URL is optional. If absent, version and version sync are `notConfigured`/`null`.

Supported response:

```json
{
  "application": "Spinner.Api",
  "version": "1.4.2",
  "commit": "93fc221...",
  "environment": "Production",
  "builtAt": "2026-08-13T12:41:00Z"
}
```

All fields are non-secret. A 2xx response with invalid JSON or an invalid/missing commit produces an
unknown version observation; it must not be treated as a successful match.

## Version synchronization

States are `inSync`, `behind`, `unknown`, or `notConfigured`.

- `inSync`: normalized full commits match.
- `behind`: GitHub ancestry/order proves that the deployed commit is an ancestor of the relevant
  source commit. Record `commitsBehind` only when GitHub proves the count.
- `unknown`: one side is missing, commits diverged, ancestry could not be proven, or a provider was
  unavailable.
- `notConfigured`: the environment has no version endpoint.

Unequal short SHAs alone never prove `behind`.

## Observations and refresh

- External facts are persisted as timestamped observations; they are not permanent truth.
- `POST /api/projects/{projectId}/refresh` is implemented before background polling and uses the same
  application use case the later worker will call.- GitHub is read once per project refresh; health/version are read per configured environment.
- One provider failure must not discard successful facts from the same refresh.
- The dashboard reads stored observations and never contacts GitHub or application endpoints during
  the dashboard request.
- Every dashboard signal carries its own observation time. The top-level `observedAt` is response
  composition time, not a claim that every fact was sampled simultaneously.
- A background sweep keeps observations current so the screens are useful without the operator pressing
  anything. It is configured under `Monitoring:Refresh`: `Enabled` (default `true`), `IntervalSeconds`
  (default 300, clamped to 30..3600), `StartupDelaySeconds` (default 10), and
  `ProjectSpacingMilliseconds` (default 500, spacing projects within one sweep). The sweep sends the same
  `RefreshProjectCommand` as the endpoint, so scheduled and manual collection cannot diverge.
- One project failing a sweep must not end the sweep, and a failed sweep must not stop the worker.
  Provider failures are recorded as observations; Console Ops is more useful stale than dead.
- Manual refresh remains: it is how an operator says "check now" rather than the only way to get data.
- A stale threshold is still not invented. Screens show each fact's observation time and let the reader
  judge.
- The refresh response returns the facts persisted by that attempt: project-level source/workflow,
  per-environment health/version/version-sync, and activities emitted by deterministic transitions.
  It never returns raw provider failures or payloads.
- Refreshes for the same project are serialized within the V1 process. Persistence rechecks the
  project's configuration version under a short database lock; if configuration changed while
  providers were being read, the attempt returns `409 Conflict` instead of attaching observations
  to stale endpoints.

## Deployment history contract

Added 2026-08-15. `GET /api/deployments?limit=` returns recorded release history, newest first. `limit`
is clamped to 1..200 and defaults to 100. Like the dashboard it reads stored records only and never
contacts GitHub during the request.

A deployment record is one run of a project's configured workflow. GitHub proves that a commit was built
and how the run ended; it does not say where the artifact landed, so no record carries an environment of
its own.

Per record: `id`, `projectId`, `projectName`, `provider` (`githubActions`), `repository`, `branch`,
`commitSha`, `commitShortSha`, `result`, `workflowFile`, `workflowName`, `workflowUrl`, `runNumber`,
`triggeredBy`, `startedAt`, `completedAt`, `deployedAt`, `durationSeconds`, `recordedAt`, and
`environments`.

- `result` uses the same vocabulary as workflow state elsewhere: `queued`, `inProgress`, `passed`,
  `failed`, `cancelled`, `unknown`. One run must never read as `Passed` on one screen and `Succeeded` on
  another.
- `deployedAt` is completion when known, otherwise start, otherwise the moment Console Ops first recorded
  the run. It is the instant the timeline orders and groups by.
- `durationSeconds` is present only when both ends are known and ordered; a negative or partial interval
  is `null`, never clamped to zero.
- `workflowUrl` is `null` unless the provider link is absolute HTTPS on a `github.com` host with no
  embedded credentials. The browser renders it as an outbound link.
- `triggeredBy` is the account login that started the run. Never a token, email, or other credential.

`environments` is evidence of where the release was observed running, not attribution:

- an entry exists because that environment's own version endpoint reported this release's commit;
- `isCurrent` is `true` while the environment's latest version observation still reports it;
- `firstObservedAt` is the first sighting, which Console Ops treats as when the release became live there
  because it is the first evidence it has;
- `healthBefore` and `healthAfter` are the health observations either side of that sighting, with their
  own observation times, and read `unknown` when no such check exists;
- `versionCheck` is the version-sync state observed once the release was seen.

An empty `environments` array means the run was recorded but the commit was never seen running anywhere.
That is a fact, not a failure, and it must not be presented as either a successful or a failed
deployment.

Collection rules:

- Records are written during `POST /api/projects/{projectId}/refresh`, inside the same transaction as
  that refresh's observations.
- The workflow-runs request that establishes workflow state reads the most recent 20 runs, so history
  costs no additional GitHub call.
- Records are upserted on `(projectId, providerRunId)`. `recordedAt` keeps the first sighting and the
  observation time keeps the last confirmation, so a run that completes between refreshes updates one row
  rather than creating a second.
- History therefore begins at the first refresh and fills in over successive refreshes. V1 makes no
  claim of gap-free history; continuous collection needs the Phase 5 worker.
- Archived projects are excluded, and an environment removed from a project stops appearing under past
  releases, because current configuration is what the operator manages.

Also collected on a schedule: it also fills release history, which is the difference between a journal
with gaps and one that records runs nobody was watching. See the collection rules above.

Not in this contract, because V1 has no source: runtime revision, runtime target, log links, and any
control that triggers, redeploys, or rolls back a release.

## Transition activity

V1 emits only deterministic changes:

- `healthFailed`: healthy/degraded to unhealthy/unreachable;
- `healthRecovered`: unhealthy/unreachable to healthy/degraded;
- `versionDrift`: in-sync to proven-behind;
- `versionSynchronized`: proven-behind to in-sync.

Unknown/not-configured transitions do not invent an incident or recovery. The first observation
establishes a baseline and does not claim a transition.

## Overview contract

`GET /api/dashboard/overview` is a purpose-built read model. Each project surface includes:

- project/environment identity;
- source revision and its observation time;
- selected GitHub Actions workflow state and its observation time;
- application health plus health observation time;
- deployed version plus version observation time;
- deterministic version-sync state and its observation time;
- latest response measurement and real historical samples, if recorded.

The endpoint returns one surface for every active project/environment pair. `project.id` identifies
the project and `environment.id` uniquely identifies the surface within the response and system-state
matrix. Project technology/type is not returned because V1 registration does not capture that fact;
the frontend uses the repository as the secondary project descriptor instead of inventing a type.

Response history is bounded to the eight most recent recorded measurements per environment and is
returned oldest first. Activity is bounded to the twenty newest deterministic transition records.
When project configuration changes, observations recorded before the new configuration timestamp are
treated as unavailable until that configuration is refreshed; they remain stored as history.

Presentation-only choices such as project-mark tone, component icons, and navigation labels are not
API fields. The frontend derives them.

The V1 system-state matrix can contain only supported rows:

- API health;
- dependencies explicitly reported by the health endpoint;
- CI;
- version sync.

Configuration and revisions are later-phase rows. Uptime is `null` until health history supports the
chosen window and calculation.

The top pipeline represents `GitHub Source -> GitHub Actions -> Application Health/Version` in V1.
It must not claim Azure runtime verification before the Azure integration exists.

## Summary aggregation

For the selected environment scope:

1. `down` if any monitored application is unreachable or unhealthy.
2. `degraded` if any application/dependency reports degraded and none is down.
3. `warning` if health is acceptable but CI failed/cancelled, proven version drift exists, or core V1
   visibility is incomplete.
4. `unknown` if no reliable current observation exists.
5. `healthy` only when all configured core V1 signals are available and acceptable.

`notApplicable` is valid for an individual cell, not the overall summary.

## Authentication and exposure gate

Development begins as a local trusted-user application. This is not permission to expose an
unauthenticated API publicly. Before hosted deployment, Console Ops must choose and implement a
single-user authentication mechanism, restrict CORS to the real frontend origin, and verify that
refresh/probe endpoints cannot be abused as arbitrary outbound-request relays.

## Phase 0 completion gate

- This document and the Angular contract agree.
- The fixture contains only V1 facts or explicit unavailable states.
- Frontend lint, formatting, tests, and production build pass.
- Backend build/tests remain green.
- No connection string, GitHub token, or credential is staged.
- Phase 1 begins with `Backend Foundation + RegisterProject`; it does not expand Phase 0 into empty
  backend scaffolding.
