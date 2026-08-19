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
completeness, logs, rollback, or Docker state. Those facts must
be omitted or reported as unavailable until their product phase is implemented.

Deployment history was added on 2026-08-15, limited to what GitHub Actions proves: workflow runs and
their outcomes, reconciled against runtime version observations. Runtime revisions remain absent.

Observed availability was added on 2026-08-16, once scheduled collection made health history continuous.
It is sampled availability with its own sample count attached, not an uptime guarantee.

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

## Azure discovery API

Added 2026-08-16 with Logs Phase 1b, so an operator picks a log source instead of typing a workspace GUID.

```text
GET /api/azure/log-sources?query=
```

Returns the applications the configured Azure identity can see, across the Azure services Console Ops can
name, each with whatever it needs to read that resource's logs:

```text
sources[]
|-- provider: azure
|-- platform: containerApp | appService
|-- name
|-- resourceGroup
|-- subscriptionId
|-- location (optional)
|-- environmentName (optional)   the Container Apps environment; absent for services with no equivalent
|-- workspaceId (optional)       null when Console Ops could not establish where the logs are sent
|-- applicationUrl (optional)    the resource's public address; null when none is reachable
`-- status: readable | noWorkspace | platformNotSupported
hasMore                          true when Azure had more than the bounded page
```

Rules:

- **Read-only inventory.** One Resource Graph query listing resources. Console Ops never creates, changes,
  or deletes an Azure resource, and needs only read access to see them.
- **Bounded.** One page of 200, ordered by platform then name, with `hasMore` so the UI can say the list is
  not everything rather than implying it is.
- **Resources Console Ops cannot read are still listed**, with `status` saying why. An operator who cannot
  find their App Service has no way to tell "Azure does not have it" from "Console Ops does not look for it",
  and that question was asked twice before this existed. Such a resource must never be offered as selectable.
- **`status` is a Console Ops fact, not an Azure one.** `platformNotSupported` means no reader exists for that
  service yet; `noWorkspace` means nothing collects the resource's logs. The platform is checked first,
  because a workspace is irrelevant while nothing can read the platform anyway.
- **App Service carries no `workspaceId`.** For Container Apps the workspace is a property of the managed
  environment and comes back in the same query. For a site it lives in a diagnostic setting that Resource
  Graph does not expose, which would cost one ARM call per site - not worth paying for a platform that has no
  reader yet.
- **`applicationUrl` is read from Azure, never composed.** An App Service host name is generated and
  unguessable, so registering a project otherwise means copying it out of the portal - which is how a project
  came to point at the StallTrack frontend instead of its API. It is `https://` plus the host: an App Service
  `defaultHostName`, or a container app's ingress FQDN **only when ingress is external**, since an internal
  FQDN resolves inside the managed environment's network and would give a project a health check that can
  never succeed. A resource with no reachable address reports `null`, and the form asks for the URL as before.
- **Failure is not emptiness.** A rejected credential returns `Azure.Unauthorized`, and the UI names the
  cause. An empty list means the identity genuinely sees no applications.
- **Filter text is escaped** into the query as a literal, never concatenated, because it arrives from a form.
- Discovery may prefill but never decide: the two configuration fields stay editable after a pick.

## Workflows API

```text
GET /api/workflows

readAt                              when the provider was asked; these are live facts, not observations
groups[]
|-- projectId, projectName, repository
|-- readFailure (optional)          why this repository could not be read; null when it was
`-- workflows[]
    |-- id, name, path
    |-- state: active | disabled
    |-- classification: deployment | unclassified
    |-- manualRun: supported | unavailable | unknown
    `-- latestRun (optional)        null when the provider reports no run
        |-- id, number
        |-- status: queued | inProgress | waiting | completed | unknown
        |-- conclusion (optional)   passed | failed | cancelled | skipped | timedOut | actionRequired | neutral
        |-- branch, commitSha, commitShortSha
        |-- trigger                 the provider's own event, verbatim
        |-- actor (optional)
        |-- startedAt, completedAt (optional)
        |-- durationSeconds (optional)
        |-- runUrl (optional)
        `-- jobs[]                  always empty here; read per selection

GET /api/workflows/projects/{projectId}/runs/{runId}/jobs

runId
jobs[]
|-- name
|-- status, conclusion (optional)
`-- durationSeconds (optional)
```

Rules:

- **Read live, bounded.** One page of workflows per repository plus one run per workflow, four run reads in
  flight at a time. Sequentially this measured **12.3s for 12 workflows across 3 repositories**; batched it is
  **4.3s warm**. Not stored, because the inventory changes when a repository changes and a stale workflow list
  answers a question nobody asked. `readAt` states when it was read.
- **Classification comes from configuration, never from the provider.** A workflow is `deployment` where its
  file name matches the project's registered deployment workflow, and `unclassified` otherwise. GitHub reports
  no business category, so `Database backup` stays unclassified however obvious its purpose looks. Verified
  live: of 12 workflows, exactly the 3 configured ones classify as deployments.
- **No environment is named.** A deployment workflow is recorded against the project, not an environment, so the
  response never claims which environment a workflow deploys. When environments own that mapping, the field
  arrives with it.
- **`status` and `conclusion` stay apart.** A run in progress has no conclusion, and none is guessed. A
  conclusion this API does not recognise is `null` rather than the nearest familiar value, and an unrecognised
  status is `unknown`.
- **`durationSeconds` is computed from the run's own start and end**, and is `null` while it is still going,
  because a duration would imply an end it has not reached.
- **`manualRun` is `unknown` until the workflow definition is read.** A dispatch trigger is declared in the
  file, not in the listing, so this reports that it does not know rather than assuming either answer. No run
  action is offered on an unknown.
- **A workflow whose latest run could not be read keeps its place** with no run rather than removing the
  workflow: the workflow was read, only its run was not.
- **An unreadable repository reports `readFailure` on its own group** and leaves the other projects intact. A
  rejected token and a repository with no automation must never look the same.
- **Jobs are a separate read.** They cost one request per run, so they are read for the workflow an operator
  selected. The repository comes from the registered project, so the read cannot be pointed at a repository
  Console Ops does not manage; an unknown project is `Workflows.ProjectNotFound`.
- **Failure is not emptiness.** `Workflows.Unauthorized`, `Workflows.RateLimited`, `Workflows.NotFound`,
  `Workflows.InvalidResponse`, `Workflows.Unavailable`. An exhausted rate limit is reported as itself, never as
  a rejected credential.

## Settings configuration status

`GET /api/settings/configuration` reports what Console Ops has been configured with. Added because a missing
GitHub token once presented as an empty repository list, which is a diagnosis nobody can make from a screen.

```text
observedAt
capabilities[]
|-- capability: Database | Source and CI | Azure | Exposure | Collection
|-- state: configured | missing | default
|-- keys[]
|   |-- key           name only, such as GitHub:Token
|   |-- state         configured | missing
|   `-- required      whether this deployment needs it
`-- connection        null unless probe=true, or when no check exists for the capability
    |-- succeeded
    `-- failure       operator wording, never a credential
probed                whether credentials were tested
about
|-- version           assembly version of the running build
|-- build             source revision, or null when the build recorded none
|-- runtime           framework description
`-- databaseSchema    upToDate | pendingMigrations | unknown
collection
|-- isEnabled         whether scheduled collection runs at all
|-- intervalSeconds
|-- lastSweepAt       null when none has run since start-up
|-- lastSweepSucceeded
|-- lastSweepMilliseconds
|-- projectsRefreshed
|-- projectsFailed
`-- nextSweepAt       null when collection is off or nothing has run yet
```

A sweep can also be asked for:

```text
POST /api/settings/collection/sweeps
```

It runs the same sweep the scheduled worker runs and reports how it went: `completedAt`, `succeeded`,
`durationMilliseconds`, `projectsRefreshed`, `projectsFailed`. It contacts every configured provider, so it is
a command rather than a read, and it is rate limited to 5 a minute.

Rules:

- **Names only, never values**, not even redacted ones. No code path reads a value into the response, and a
  test sets a sentinel into three keys and asserts the body does not contain it.
- **Cheap by default.** The plain read inspects configuration only, measured at under a second. `?probe=true`
  additionally contacts each provider - a database connection, a GitHub call, an Azure token - measured at
  about five seconds on a cold Azure credential. A screen loads with the cheap read and offers the probe as an
  explicit action.
- **`state` is a verdict about a capability**, from the worst of its keys. A missing required key decides it. A
  key that is optional and unset reads as `default`, because something else stands in for it: the ambient
  Azure identity, or a built-in value.
- **`required` is contextual.** `Api:Key` is required only when Console Ops is bound somewhere other than
  loopback, decided by the same `NetworkExposure` rule as the startup guard so the two cannot drift.
- **A failed check is not a failed request.** One unreachable provider is reported as a failed connection and
  must not hide the state of the others; a probe that throws reports a check that could not be completed.
- **Absent is not failure.** `connection: null` means no check ran, and the UI must not render it as a problem.
- **Sweeps are remembered for one process.** A sweep describes Console Ops, not a project, so it is not written
  to the observation tables. After a restart `lastSweepAt` is null and the screen says none has run since
  start-up rather than filling one in.
- **`nextSweepAt` is derived from the last sweep's start**, because the schedule's timer runs from there. It is
  an expectation, shown as approximate, and null when collection is off - which is a configuration, not a fault.
- **`retention` reports what Console Ops deletes.** Collection appends a row per environment per sweep and every
  screen reads a bounded window, so rows past the window cost storage without answering anything. The window is
  configuration with a floor of one week, deletes are batched, and every sweep reports how many rows it removed
  and the cut-off it used. A sweep that has not run reports nothing rather than implying it removed zero, and
  disabling retention means nothing is ever deleted.
- **`about` is read from the running process**, never configured: version and revision from the assembly,
  runtime from the framework description, schema from the pending-migration list. A database that could not be
  asked reports `unknown`, which is not the same as `upToDate`.

## Health overview contract

`GET /api/health` reports the recorded health of every active environment. Reads only: it reports what
collection already wrote, so opening the screen never causes an application to be probed.

```text
observedAt
summary
|-- healthy | degraded | down     counts by what an operator would act on
`-- lastCheckedAt                 null when nothing has been checked at all
environments[]
|-- id, projectId, projectName, environmentName, environmentKind
|-- state                         the seven states, camel case; unknown when no check exists
|-- checkedAt                     null when never checked
|-- responseMilliseconds
|-- checks[]                      the application's own check first, then each dependency it reported
|   |-- name, kind                kind is a display hint: application | database | cache | external | unknown
|   |-- state
|   `-- responseMilliseconds      the application reports a round trip; a dependency reports only a state
|-- healthySince | failingSince   the current unbroken run, derived from the sequence, null when it does not apply
|-- consecutiveFailures
|-- lastHealthyAt
`-- window                        the last 24 hours
    |-- availabilityPercentage    null below the minimum sample
    |-- checks | failedChecks
    `-- longestOutageSeconds      null when nothing failed, which is not the same as zero
stateChanges[]                    transitions as recorded, newest first
```

Rules:

- **An unchecked environment is not healthy.** It reports `unknown` with a null `checkedAt`, and is counted in
  none of the summary columns: it is not evidence of being up or down.
- **A run is derived from the sequence, not stored.** `healthySince` is null unless the latest check is healthy,
  and a run that began before the window reports its earliest known check rather than claiming more.
- **Availability comes from one definition**, shared with the dashboard: a degraded application answered, so it
  counts as served, while an indeterminate check counts on neither side. Below the minimum sample there is no
  figure at all.
- **Transitions are read, never re-derived.** They were recorded at the moment they happened, which is the only
  honest way to report a change after the fact.
- **Degraded is an active issue but counts as available.** Those answer different questions and both are true.
- **Dependencies are what the application said.** An empty list means it reported none, not that all is well.

## Transport rules
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
    |-- versionUrl (optional)
    `-- logSource (optional)
        |-- provider: azureContainerApps      (response only)
        |-- workspaceId                       Log Analytics workspace GUID
        `-- containerAppName
```

Repository owner/name/default branch are required. `workflowFile` is an explicit GitHub Actions
workflow file name such as `ci.yml`; if absent, CI is `notConfigured`. Console Ops must not select an
arbitrary latest workflow.

`logSource` says where an environment's application logs are read from, added 2026-08-16 with Logs
Phase 1. It is configuration, not a credential: Console Ops authenticates to Azure from its own settings,
so no secret is ever stored on a project. Both parts are required together - half a source cannot be
queried, so the domain refuses it and the API returns a validation problem rather than storing something
that could only fail. `containerAppName` must be a name Azure would accept, because it reaches a provider
query. Omitting `logSource`, or sending it as `null` on update, means the environment has no log source and
reads as not configured.

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

## Log stream contract

`GET /api/logs` reads one project environment's console output from its provider during the request, and is
Console Ops' only pass-through read. Query parameters: `projectId`, `environmentId`, `search`, `limit`,
`before`. The full behavior is specified in `Console_Ops_Logs_Plan.md`; the transport rules are here.

`items` is a **tagged union, discriminated by `kind`**, in one ordered list so a marker keeps its position in
time:

- `kind: "event"` - one line of console output. `level` is one of seven values with `unknown` for a line
  carrying no recognizable prefix, and `levelIsDerived` says whether Console Ops parsed the level rather
  than the emitter declaring it. `receivedAt` sits beside `occurredAt` so ingestion delay stays visible.
- `kind: "marker"` - context Console Ops derived from what it already recorded. `markerKind` is
  `deployment`, `revision`, or `containerRestart`.

Rules that keep markers honest:

- A marker is composed at query time from the `deployments` rows a refresh already wrote. There is no marker
  table, nothing is written to a log store, and the Deployments screen reads the same rows.
- A deployment marker carries `commitShortSha` and `deploymentId`, and `revision` is always `null`: a run
  proves CI built a commit, not that a revision started serving it.
- A revision marker carries `revision` only, once per revision, at the earliest line Console Ops has from
  it. The revision already serving when the window opened is not marked. The wording is "first seen", never
  "started": console output says which revision emitted a line, not when it began serving.
- Markers are bounded by the events actually returned, not by the requested window, so a marker never sits
  below the oldest visible line.

`noise` states what was left out to make the stream readable: `{ excluded, hiddenCount }`. Framework
categories - EF Core, the `HttpClient` factory, routing - are excluded by default at **information and
below**, because an idle service logs almost nothing else. A warning or worse is never hidden whatever
emitted it, an unparsed line is never assumed to be unimportant, the count is always reported, and
`includeNoise=true` returns everything. Filtering is pushed to the provider for the same reason as search:
removing noise after the fact would only empty the page.

Paging backwards uses `before` as a **time cursor**,because a time is what the provider can seek on. The
window bound is inclusive and two console lines can share a millisecond, so pages are merged **by id**: an
exclusive time cursor would silently drop every line that shared the boundary instant. Verified against the
real workspace - a second page reached back a further eight minutes with no duplicate ids. The screen keeps
every page it has read, so the 30-second re-read cannot discard what was paged in.

The discriminator is emitted by the serializer rather than by hand. It was once omitted while every typed
test still passed, and a correct response with seventeen events rendered as an empty stream; a test now
asserts it on the raw JSON.

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

Configuration and revisions are later-phase rows.

`summary.uptime` reports availability as Console Ops sampled it, not a provider's guarantee:

- window: the last 24 hours, sent as `windowHours` and `since` so the UI supplies the wording;
- `percentage`: share of measured checks that were acceptable, to one decimal, never rounded up to a
  flattering 100;
- `checks`: how many measured checks the figure rests on, so the screen can say what it is based on;
- `samples`: availability per hour, oldest first, only for hours containing checks. An hour with no check
  is absent rather than drawn as zero or as full availability.

A check that established nothing - unknown state, or no health endpoint configured - counts on neither
side of the ratio. Below 12 measured checks the whole window is `null`: three checks in a day can produce
a confident-looking 100%, which would be the most misleading number on the screen. Availability is
therefore a consequence of scheduled collection, and it appears roughly an hour after collection starts.

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
