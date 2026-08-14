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
- the read-only Overview query.

V1 does not claim Azure revision state, deployment history, restart/migration/container events,
configuration completeness, logs, rollback, uptime percentages, or Docker state. Those facts must
be omitted or reported as unavailable until their product phase is implemented.

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

`DELETE` archives a project in V1. Normal list/dashboard queries exclude archived projects.

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
  application use case the later worker will call.
- GitHub is read once per project refresh; health/version are read per configured environment.
- One provider failure must not discard successful facts from the same refresh.
- The dashboard reads stored observations and never contacts GitHub or application endpoints during
  the dashboard request.
- Every dashboard signal carries its own observation time. The top-level `observedAt` is response
  composition time, not a claim that every fact was sampled simultaneously.
- A background interval and stale threshold remain configurable Phase 5 decisions. Phase 0 does not
  invent fixed freshness guarantees before the worker exists.

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
