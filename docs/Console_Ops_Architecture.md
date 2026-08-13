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
`-- Features/
    `-- Projects/
        |-- RegisterProjectEndpoint.cs
        |-- GetProjectEndpoint.cs
        `-- ProjectEndpoints.cs
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

Use ASP.NET Core `IExceptionHandler` plus `AddProblemDetails` for centralized unexpected-error
responses. Map validation exceptions if the pipeline throws them. Log the full exception once while
returning a safe problem response with a trace identifier. Do not return stack traces, provider
payloads, URLs containing credentials, tokens, or connection details.

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
