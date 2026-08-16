# Console Ops Architecture

## Status and intent

This document is the architecture baseline for Console Ops. It complements
`Console_Ops_Project_Context.md`, which remains the source of truth for product behavior and
scope.

The architecture is deliberately pragmatic: a modular monolith, separated into a few clear
projects, with vertical slices for use cases, CQRS through MediatR, and DDD where business rules
and language benefit from it. It does not use microservices, event sourcing, a generic repository,
or abstractions that have no current boundary.

## Architectural drivers

1. Return trustworthy operational state and say `Unknown` when truth cannot be established.
2. Make the project and environment explicit in data, APIs, logs, and future actions.
3. Keep provider details behind boundaries so GitHub, HTTP probes, Azure, and local agents do not
   leak their SDK models into the domain.
4. Protect credentials and operational data by default.
5. Deliver V1 as small end-to-end slices before adding deeper runtime integrations or controls.
6. Keep the design testable without turning every class into an interface.

## Architecture decision

Use one deployable ASP.NET Core API backed by PostgreSQL. Organize the backend into these projects:

```text
repos/backend/
|-- ConsoleOps.Api/             HTTP boundary and composition root
|-- ConsoleOps.Application/     vertical use-case slices and ports
|-- ConsoleOps.Domain/          domain model and invariants
|-- ConsoleOps.Infrastructure/  EF Core and external-provider adapters
|-- ConsoleOps.Tests.Unit/      domain and application unit tests
`-- ConsoleOps.Tests.Integration/ API, database, and adapter integration tests
```

The current `ConsoleOps.Tests` project may be evolved into these test projects when the foundation
is implemented. Do not reorganize merely to make the tree look complete; make the change together
with the first working slice.

### Dependency direction

```text
ConsoleOps.Domain          -> no project dependency
ConsoleOps.Application     -> ConsoleOps.Domain
ConsoleOps.Infrastructure  -> ConsoleOps.Application + ConsoleOps.Domain
ConsoleOps.Api             -> ConsoleOps.Application + ConsoleOps.Infrastructure
```

Domain and Application must not depend on ASP.NET Core, provider SDKs, or Infrastructure.
Infrastructure is registered only from the API composition root.

## Multi-agent Git workflow

When Claude, Kiro, Codex, or another coding agent edits Console Ops concurrently, use one Git
worktree and one task branch per actively editing agent. This is the default for parallel changes,
not a requirement for read-only review or sequential work performed by one agent at a time.

Git worktrees require the Console Ops root to be a Git repository. Until it is initialized, agents
cannot use this workflow safely. Keep worktrees beside the primary checkout rather than nesting them
inside it:

```text
C:\dev\ConsoleOps                                  primary integration worktree
C:\dev\ConsoleOps-worktrees\codex-register-project
C:\dev\ConsoleOps-worktrees\claude-github-adapter
C:\dev\ConsoleOps-worktrees\kiro-angular-shell
```

Create each worktree from the agreed integration branch with a descriptive agent/task branch:

```powershell
git worktree add ..\ConsoleOps-worktrees\codex-register-project `
  -b agent/codex/register-project main

git worktree add ..\ConsoleOps-worktrees\claude-github-adapter `
  -b agent/claude/github-adapter main
```

Use the actual integration branch if it is not `main`.

### Coordination rules

- Assign a clearly bounded slice or concern to each worktree. Do not have two agents edit the same
  slice, migration, package file, or shared contract concurrently unless the overlap is coordinated.
- Keep the primary worktree for integration and final verification. Parallel agents should not make
  uncommitted edits there.
- Begin by checking branch/worktree status and reading the current product and architecture docs.
- Commit focused, reviewable changes on the task branch. Exchange work through commits and normal
  Git integration, never by copying another agent's partially edited working directory.
- Rebase or merge the latest integration branch before handoff when appropriate, then resolve
  conflicts in the task worktree rather than hiding them in the integration worktree.
- Integrate one completed task at a time and run the relevant build/tests after each integration.
- Nominate one owner for EF Core migrations in a parallel batch. A worktree isolates files but does
  not isolate a shared development database, containers, ports, or external provider state.
- Give concurrently running API/frontend instances unique ports and use separate disposable test
  databases or schemas when tests can mutate shared state.
- Do not copy untracked secrets automatically between worktrees. Configure required credentials
  through approved user-secrets or environment mechanisms in each execution context.
- Remove a worktree only after its changes are committed, integrated or intentionally abandoned,
  and verified. Never remove a worktree while its agent is active.

This workflow reduces filesystem collisions; it does not replace task ownership, small slices,
review, or integration testing.

## Vertical slices and CQRS

Organize application behavior by capability and use case, not by broad technical folders such as
`Services`, `Dtos`, or `Repositories`.

```text
ConsoleOps.Application/
|-- Abstractions/
|-- Behaviors/
|-- Common/
`-- Features/
    `-- Projects/
        |-- RegisterProject/
        |   |-- RegisterProjectCommand.cs
        |   |-- RegisterProjectHandler.cs
        |   |-- RegisterProjectValidator.cs
        |   `-- RegisterProjectResult.cs
        |-- GetProject/
        `-- ListProjects/

ConsoleOps.Api/
|-- Extensions/                 focused API-boundary helpers
|-- Features/
|   `-- Projects/
|       |-- RegisterProjectEndpoint.cs
|       |-- GetProjectEndpoint.cs
|       `-- ProjectEndpoints.cs
`-- Middleware/                 cross-cutting HTTP pipeline behavior
```

Each endpoint maps transport input to one command or query, sends it through `ISender`, and maps the
result to an HTTP response. Business decisions belong in the handler or domain model, not in the
endpoint.

- Commands change state and use imperative names such as `RegisterProjectCommand`.
- Queries read state and return purpose-built response models.
- CQRS means separate request models and handlers; it does not mean separate databases or event
  sourcing.
- Pass `CancellationToken` through endpoints, MediatR, EF Core, and outbound HTTP calls.
- Avoid commands that accept `HttpContext`, `IFormFile`, claims principals, or provider SDK types.

Keep a slice cohesive. Shared code moves to `Common` only after at least two real consumers need the
same concept.

## DDD boundaries

Use DDD for domain language and invariants, not as a reason to manufacture layers.

Likely V1 concepts include:

- `Project` as the registration aggregate.
- `ProjectEnvironment` for a named local/development/staging/production target.
- value objects for repository coordinates, endpoint addresses, commit identifiers, and environment
  kind where they enforce useful validity.
- monitoring observations/read models for source, CI, health, deployed version, and sync state.
- activity records emitted from deterministic state transitions such as health failure/recovery or
  drift detection/synchronization.

Rules that belong to the domain must be enforced by constructors/factories or methods. Public
setters must not allow invalid aggregate state. Use domain events only when a real in-process side
effect needs decoupling; do not add them pre-emptively.

`Environment` is easily confused with `System.Environment`; prefer the explicit domain name
`ProjectEnvironment`.

## Persistence

- Use EF Core with Npgsql in Infrastructure.
- Keep `DbContext`, entity configurations, and migrations in Infrastructure.
- Use explicit EF configurations rather than filling entities with persistence attributes.
- Prefer `AsNoTracking` and direct projection for queries.
- Use an application persistence abstraction or an aggregate-specific repository where it improves
  the boundary. Do not add a generic `IRepository<T>` wrapper over EF Core.
- Keep transactions at the command/use-case boundary. One command should normally call
  `SaveChangesAsync` once.
- Use optimistic concurrency when project configuration can be edited concurrently; do not add a
  distributed transaction.
- Store timestamps in UTC and inject `TimeProvider` where time affects behavior.

The existing `DefaultConnection` setting can be used by Infrastructure, but credentials must come
from development user-secrets/environment variables or a production secret store. Never copy a
connection string into documentation, source, logs, tests, or API responses.

## Result and error model

Use an immutable application result for expected outcomes:

```text
Result
|-- IsSuccess
`-- Error(Code, Description, Type)

Result<T>
|-- Value (success only)
`-- Error (failure only)
```

Use stable error codes such as `Projects.NotFound`; do not make callers parse prose. Error types may
include Validation, NotFound, Conflict, Unauthorized, Forbidden, and Failure.

The result must not contain HTTP status codes. Application and Domain do not know HTTP. At the API
boundary, one mapping extension converts error types to RFC 7807 `ProblemDetails`/typed results.
Validation details should use `ValidationProblemDetails`.

- Return results for expected business failures.
- Throw exceptions for unexpected infrastructure/programming failures.
- Let FluentValidation handle request shape and cheap application-level rules.
- Let aggregates enforce domain invariants even if input validation already ran.

## Validation and exception handling

Register one MediatR validation behavior. It must call `next()` exactly once. If there are no
validators, return `await next()` immediately. Run validators asynchronously with the supplied
cancellation token and combine their failures.

While the API is small, use one `Middleware/ExceptionMiddleware.cs` plus `AddProblemDetails` for
centralized validation and unexpected-error responses. Log the full unexpected exception once while
returning a safe problem response with a trace identifier. Split into multiple ASP.NET Core
`IExceptionHandler` implementations only when distinct handling policies make the single middleware
materially difficult to maintain. Do not return stack traces, provider payloads, URLs containing
credentials, tokens, or connection details.

Use descriptive API folders such as `Features`, `Middleware`, and `Extensions`. Do not create an
`Infrastructure` folder inside `ConsoleOps.Api`; the separate `ConsoleOps.Infrastructure` project
already owns persistence and provider infrastructure. Add `Common` or `Abstractions` inside the API
only after a concrete shared API-boundary concern requires them.

Middleware order must be deliberate and contain no duplicate authentication/authorization calls.

## External integrations

Application defines narrow capability ports, for example:

- a GitHub source/CI reader,
- an application health/version probe,
- later, an Azure Container Apps reader.

Infrastructure implements them using typed `HttpClient` instances or provider SDKs. Keep external
DTOs inside the adapter and map them to application-owned models. Configure explicit timeouts,
cancellation, safe logging, and modest resilience policies. Never retry every failure blindly,
especially authentication, validation, or destructive operations.

The V1 GitHub adapter reads an optional token from `GitHub:Token` (environment-variable form
`GitHub__Token`) and an optional bounded request timeout from `GitHub:TimeoutSeconds`. Keep the token
in user-secrets, environment configuration, or a production secret store; do not add it to an
appsettings file. Public repositories can be read without a token, subject to GitHub's limits.

Application health/version probes reject redirects and non-HTTP(S) URLs, cap response bodies, and
pin each connection to DNS addresses that were checked as public. Loopback, link-local, private,
multicast, documentation, and other special-purpose address ranges are blocked by default. A trusted
local installation may explicitly allow exact private host names or IP addresses with
`ApplicationProbes:AllowedPrivateHosts` (environment-variable form
`ApplicationProbes__AllowedPrivateHosts`, comma/semicolon separated). Do not enable a private host
for an Internet-exposed installation; network egress controls remain a required defense in depth.

The runtime/provider remains the source of truth. A database row is a cached observation with a
timestamp, not proof of current health.

Use one refresh use case that can be invoked by an endpoint now and a background worker later. Avoid
duplicating probe/correlation logic. Bound concurrency so one slow project cannot exhaust outbound
connections.

## Deterministic correlation

Calculate high-level status from explicit facts:

```text
source commit + workflow state + deployed commit + health result + observation times
                                  |
                                  `-> operational summary and activity transitions
```

- `InSync`: normalized deployed commit matches the relevant source commit.
- `Behind`: a deployed commit is known and determined to be older than the relevant source.
- `Unknown`: either side cannot be established safely.

Do not claim `Behind` from unequal short SHAs alone unless ancestry/order is known. Do not use AI to
invent causation. Persist enough provenance and timestamps to explain every displayed conclusion.

## API conventions

- Use Minimal API route groups by feature unless a controller provides a concrete benefit.
- Use resource-oriented routes and standard verbs; action routes are reserved for real operations.
- Return typed contracts; never serialize EF entities or provider SDK objects.
- Use consistent RFC 7807 errors, OpenAPI metadata, and cancellation.
- Add API authentication only when exposure requires it; do not build enterprise RBAC for the
  initial single-user product.
- Keep operational write actions out of V1.

## Testing strategy

- Domain unit tests cover invariants and sync/attention-state calculations.
- Handler tests cover expected success/failure decisions where they add value.
- Integration tests use the real API pipeline and PostgreSQL-compatible behavior for persistence
  slices; mock only true external systems at their HTTP/adapter boundary.
- Adapter contract tests use representative GitHub, health, and version payloads.
- Every bug fix receives a regression test at the narrowest useful level.

Avoid tests that merely verify MediatR or EF Core itself. Prefer behavior and public outcomes.

## Angular direction (later phase)

Create the Angular application only when the backend foundation and initial contracts are stable.
Use the latest Angular CLI at that time, standalone components, routing, SCSS, strict TypeScript, and
a feature-first layout. Start with Angular signals/services and typed API contracts; do not add NgRx
until state complexity proves it necessary.

The supplied dashboard image is visual direction, not literal product truth:

- use `Console Ops`, never `DevDeck`;
- show GitHub Actions for the initial CI provider, not Azure DevOps;
- preserve the restrained, table-led, information-dense visual language;
- prioritize V1 facts and honest unavailable/unknown states over placeholder metrics.

### Sequencing decision: mock-backed Overview built before the API

Decided 2026-08-14. The Angular workspace and the Overview (home) screen are built first, ahead of
the backend slices, to settle the dashboard contract and visual language early. This is an accepted
deviation from the recommended implementation order below and applies to the Overview screen only.

Rules that keep the deviation safe:

- The frontend consumes a typed contract that mirrors the intended `GET /api/dashboard/overview`
  response. `Console_Ops_V1_API_Contract.md` freezes the transport semantics, and the TypeScript
  contract mirrors it for the later backend query.
- Data reaches the UI only through an injectable data-source port. V1 registers a mock adapter; the
  HTTP adapter replaces it at the provider registration with no component changes.
- Mock data must obey the product rules: `Unknown`, `N/A`, and `Not configured` states are present,
  and no metric is invented for a capability the product does not yet measure.
- The mock stays clearly identifiable as a fixture and is not promoted into a runtime fallback that
  could mask a real API failure.
- Backend work continues in its documented order. When the dashboard query slice lands, the mock
  adapter is removed rather than kept in parallel.
- Phase 0 removes presentation-only API fields and later-phase mock claims before persistence or
  backend response types are created.

### Projects screen contract reconciliation

Decided 2026-08-14. The design-first Projects and Add Project screens are now connected to the
implemented V1 project APIs. Their runtime mock adapter was removed, and their TypeScript contracts
mirror `GET /api/projects` and `POST /api/projects` exactly.

The design proposal to add application `kind`, runtime/framework, deployment history, lifecycle, and
separate monitoring-intent flags was rejected for V1. Those fields and columns were removed rather
than populated with guesses or added to the backend without a product requirement. Normal project
queries continue to exclude archived projects. The Projects screen shows persisted project,
repository, environment, workflow, endpoint-presence, timestamp, and configuration-version facts.

Add Project supports the optional explicit GitHub Actions workflow file already present in the V1
contract. Relative health/version paths are a form convenience only: the frontend resolves them
against the configured application base URL and sends only absolute HTTP(S) URLs to the API. After a
successful registration, the frontend requests one best-effort project refresh so the dashboard can
show stored observations. A refresh failure does not reinterpret a successful registration as a
failure. Azure remains labelled as a later phase because Azure runtime awareness is V2.

### Project editing and archiving reached the UI

Decided 2026-08-15. `PUT /api/projects/{id}` and `DELETE /api/projects/{id}` existed and were tested but
unreachable, leaving two V1 capabilities - edit configuration, archive a project - implemented and
unusable. Both are now exposed at `/projects/{id}/edit`.

The edit form always sends the complete repository and environment list, because the endpoint replaces
editable configuration rather than patching it, and it carries the `configurationVersion` it loaded. A
stale version comes back as a conflict, which the screen reports as "this project changed since you
opened it" instead of retrying and overwriting someone else's change. Existing environments keep their
`id` so the API matches them rather than recreating them.

Archiving takes two deliberate steps: the first press asks, naming the project and saying what archiving
means, and only the second calls the API.

Environments can be added and removed in the same form, within the contract's rules. A new environment is
sent without an `id`, which is how the API knows to create it; an existing one keeps its `id` so it is
matched rather than recreated. Removing an environment that exists asks first and says its observations
are discarded, while an unsaved draft goes immediately because it has nothing to lose. The last
environment cannot be removed, since every project must keep one, and duplicate names are rejected before
the API has to.

### Environments screen contract reconciliation

Decided 2026-08-15. The Environments screen is connected to V1 truth and its fixture, port, adapter and
store were deleted. It is composed in the frontend from two existing endpoints rather than a new one:
configuration from `GET /api/projects` and observations from the stored dashboard overview, joined on
environment id.

The design proposal to add runtime provider and target, Azure revision, configuration-presence counts,
and last-deployment time was **not** adopted for V1, matching the Projects reconciliation above. Those
columns were removed rather than populated with guesses, and the archived-environments view went with
them because normal V1 queries exclude archived projects and no archive query exists.

What the screen now shows per environment: project, environment name and kind, application URL, health,
version sync, deployed version, health endpoint, version endpoint, and last check. An environment with
no observation reads `Not observed yet` rather than borrowing another environment's state. Editing is
offered as a link to the project, because V1 edits environments through project configuration
replacement rather than an environment resource.

### Deployments screen: release history recorded from GitHub Actions runs

Decided 2026-08-15, replacing the fixture-backed design mock of 2026-08-14. The screen reads
`GET /api/deployments`; the fixture, its mock adapter, and the SAMPLE DATA banner are deleted.

A deployment record is **one run of a project's configured workflow**. That is the honest unit: GitHub
proves a commit was built and how the run ended, not where the artifact landed. There is deliberately no
environment column on the `deployments` table.

The environment link is **evidence, not attribution**. An environment appears under a release because its
own `/version` endpoint reported that release's commit, and the release is `isCurrent` there while that
is still the latest thing the environment reported. This reuses the deterministic commit match already
behind version sync. A release with no environments was built but never seen running: the screen says
`Not observed running` rather than claiming or hiding a destination, and such a release drops out of an
environment-scoped view instead of being assumed into it.

Health before and after are the health observations bracketing the first sighting of the commit in that
environment. This is only possible because the observation tables are append-only, and it is what makes
a bad release visible: `Healthy` before, `Unhealthy` after, same environment, recorded times shown.

Collection is bounded and idempotent. Refresh reads the last `WorkflowRunPageSize` (20) runs from the
same workflow-runs request that already answers "what is the workflow doing", so history costs no extra
GitHub call. Records are upserted on `(project_id, external_run_id)`: `recorded_at_utc` keeps the first
sighting, `observed_at_utc` the last confirmation, so an in-flight run completing updates one row.
History therefore starts at the first refresh and fills in as refreshes continue; continuous collection
needs a background worker and is a later phase.

Two rules carried over from the mock still hold:

- The verification verdict is derived, not stored. `core/ui/deployment-verdict.ts` computes it from the
  run outcome, the health observed after the release was seen, and version sync, in severity order. Drift
  is only reported for an environment the release still serves, because a superseded release is expected
  to be behind. An unknown fact yields `Unverified` rather than a pass.
- Every figure in the verification summary is counted from the records in view, never estimated. When a
  figure has no basis it reads as unavailable rather than zero, and it recounts when the view narrows.

Still absent, because V1 has no source: the runtime revision (`spinner-api--000021`) and the runtime
target, both of which need Azure awareness. Triggering, redeploying, rolling back, and log access remain
disabled controls that name the phase they belong to.

### Collection is scheduled; the browser only re-reads

Decided 2026-08-15. Console Ops collects observations on its own so the screens are current without the
operator pressing anything. Two halves, deliberately separated:

- **The API collects.** `ProjectRefreshWorker` sweeps active projects on an interval (`Monitoring:Refresh`,
  default every 300s) and sends the same `RefreshProjectCommand` the manual endpoint sends. There is no
  second collection path that could record different facts or emit different activity. A project that
  fails is logged and skipped; the sweep continues and the worker survives. Registration is conditional
  on `Enabled`, so collection can be turned off entirely, and integration tests turn it off so they
  assert only the refreshes they perform.
- **The browser re-reads.** `core/state/auto-refresh.ts` re-reads a screen's stored data every 30s while
  it is being looked at. It never asks the API to contact a provider. A hidden tab is not polled, and
  returning to one reads immediately rather than showing state from minutes ago.

The manual refresh action stays. Its meaning changes from "the only way to get data" to "check now",
which is worth having when a deploy is in flight.

This also matters for release history: with only manual refreshes, a workflow run that starts and ends
between two visits is never recorded. A steady sweep is what makes the Deployments timeline continuous
and gives health-before and health-after something to compare.

Freshness is still not asserted. No stale threshold is invented; every screen shows each fact's own
observation time and lets the reader judge.

### Add Project: import-first direction

Decided 2026-08-14. Registration should discover whatever a provider already knows and ask the operator
only for what no provider can know. The phased plan lives in `Console_Ops_Add_Project_Import_Plan.md`:
Phase 0 is the information architecture and is implemented; Phase 1 adds repository discovery, Phase 2
workflow discovery, Phase 3 pre-registration endpoint verification, and Phase 4 a confirmation step and
Azure runtime import.

Two rules from that plan bind any agent touching this screen. Discovery may prefill but never silently
decide, so a suggested workflow is still confirmed by the operator. And no probe result may appear on
this screen until an endpoint actually returns one - pre-registration verification is server side,
through the existing probe safeguards, never from the browser.

## AMYL.Api reference policy

The AMYL.Api project is a pattern reference, not a template to copy wholesale.

Retain the useful concepts: feature slices, MediatR, FluentValidation, EF Core/Npgsql, centralized
error handling, extension-based registration, and small endpoint mappings.

Correct these issues in Console Ops:

- validation must never invoke a handler twice;
- authentication/authorization checks must be covered by tests and use correct conditions;
- results must be immutable and transport-agnostic;
- commands and queries must not depend on ASP.NET types;
- register `DbContext` once and avoid duplicated middleware;
- use safe Problem Details and consistent response semantics;
- use cancellation and async I/O end to end;
- do not add caching, Redis, JWT, rate limiting, file services, or telemetry merely because the
  reference project has them.

## Guardrails against overengineering

Do not introduce microservices, a message broker, event sourcing, a mediator wrapper, a generic
repository/unit-of-work pair, a plugin framework, multi-tenancy, enterprise RBAC, Redis, or a local
agent before a concrete requirement needs it.

Prefer one complete tested slice over a large empty folder scaffold. Every abstraction needs either
a domain meaning, a test seam at an external boundary, or more than one implementation.

## Recommended implementation order

1. Establish the four backend project boundaries, common result/error mapping, exception handling,
   validation behavior, PostgreSQL persistence, and test harness.
2. Implement `RegisterProject`, `UpdateProject`, `ArchiveProject`, `GetProject`, and `ListProjects`
   as complete slices.
3. Add the GitHub source/CI adapter behind an application port.
4. Add safe health and version probes behind an application port.
5. Implement project refresh, deterministic version-sync calculation, and transition-based activity.
6. Add the dashboard query contract.
7. Build the Angular shell and V1 dashboard against real API contracts.

Each step must build and test before the next capability is added.

The mock-backed Overview screen described under "Angular direction" runs ahead of this order by
decision. Step 7 then reduces to replacing the mock data-source registration with the HTTP adapter
and reconciling any contract differences found in step 6.
