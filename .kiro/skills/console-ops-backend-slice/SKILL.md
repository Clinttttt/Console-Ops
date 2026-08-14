---
name: console-ops-backend-slice
description: Recipe for adding or reviewing a Console Ops backend feature - vertical slice layout, MediatR command/query handlers, FluentValidation, Result/error mapping to ProblemDetails, EF Core persistence, and external provider ports. Use when writing, moving, or reviewing anything under repos/backend.
---

# Console Ops backend slice recipe

Authority: `docs/Console_Ops_Architecture.md` for boundaries and
`docs/Console_Ops_V1_API_Contract.md` for V1 transport behavior. This skill is the working
checklist, not a new rule set. Read the docs when a question is not answered here.

## Project boundaries

```text
repos/backend/
|-- ConsoleOps.Api/             HTTP boundary + composition root
|-- ConsoleOps.Application/     use-case slices and ports
|-- ConsoleOps.Domain/          aggregates, value objects, invariants
|-- ConsoleOps.Infrastructure/  EF Core + provider adapters
|-- ConsoleOps.Tests.Unit/
`-- ConsoleOps.Tests.Integration/
```

`Domain -> nothing`, `Application -> Domain`, `Infrastructure -> Application + Domain`,
`Api -> Application + Infrastructure`. Infrastructure is registered only from the API composition
root. Create the split alongside the first real slice, not as an empty scaffold.

## Slice layout

Organize by capability and use case. Never by `Services`, `Dtos`, or `Repositories`.

```text
ConsoleOps.Application/Features/Projects/RegisterProject/
|-- RegisterProjectCommand.cs
|-- RegisterProjectHandler.cs
|-- RegisterProjectValidator.cs
`-- RegisterProjectResult.cs

ConsoleOps.Api/Features/Projects/
|-- ProjectEndpoints.cs        route group
`-- RegisterProjectEndpoint.cs
```

Shared code moves to `Application/Common` only after two real consumers need the same concept.

## Checklist for a new slice

1. Name it after the use case. Commands are imperative (`RegisterProjectCommand`); queries return
   purpose-built response models.
2. Request models carry primitives and application-owned types only — no `HttpContext`, `IFormFile`,
   `ClaimsPrincipal`, or provider SDK types.
3. Enforce domain invariants in constructors, factories, or methods. No public setter may allow an
   invalid aggregate state.
4. Validate request shape and cheap rules with FluentValidation. The validation behavior calls
   `next()` exactly once and returns `await next()` immediately when no validators exist.
5. Return `Result`/`Result<T>` with a stable code for expected outcomes. Throw only for unexpected
   infrastructure or programming faults.
6. The endpoint maps transport input to one command/query, sends it through `ISender`, and maps the
   result with the shared error-to-`ProblemDetails` extension. No business decisions in endpoints.
7. Pass `CancellationToken` end to end. Call `SaveChangesAsync` once per command.
8. Add tests: domain invariants and state calculations as unit tests, handler decisions where they
   add value, the slice through the real API pipeline for persistence behavior.

## Persistence

- EF Core + Npgsql in Infrastructure; `DbContext`, explicit entity configurations, and migrations live
  there. Register `DbContext` once.
- Queries use `AsNoTracking` and project directly into response models. Never serialize an EF entity.
- Aggregate-specific persistence abstractions are allowed when they improve the boundary; a generic
  `IRepository<T>` over EF Core is not.
- Optimistic concurrency where project configuration can be edited concurrently. UTC timestamps.
- One agent owns migrations per parallel batch. A worktree does not isolate a shared database.

## External providers

Application declares a narrow capability port (GitHub source/CI reader, health/version probe, later
an Azure Container Apps reader). Infrastructure implements it with a typed `HttpClient` or SDK and
keeps provider DTOs internal to the adapter.

- Explicit timeouts, cancellation, bounded concurrency, modest resilience.
- Never blindly retry authentication, validation, or destructive calls.
- One refresh use case serves both an endpoint now and a background worker later.
- Safe logging only: no tokens, credentialed URLs, or raw provider payloads.

## Deterministic correlation

Compute status from explicit facts: source commit + workflow state + deployed commit + health result
+ observation times. `InSync` on normalized commit match; `Behind` only when ancestry/order is known;
otherwise `Unknown`. Persist provenance and timestamps so every displayed conclusion is explainable.
Never infer causation with AI.

## API conventions

Minimal API route groups per feature, resource-oriented routes, standard verbs, typed contracts,
OpenAPI metadata, RFC 7807 errors (`ValidationProblemDetails` for validation), deliberate middleware
order with no duplicated auth calls. Add authentication when exposure requires it; no enterprise RBAC
for a single-user product. No operational write actions in V1.

## AMYL.Api reference

Reference for ideas only. Copy the useful shape (feature slices, MediatR, FluentValidation, EF
Core/Npgsql, centralized error handling, extension-based registration, small endpoint mappings) and
correct its defects: double handler invocation from validation, untested/incorrect auth conditions,
mutable or transport-aware results, ASP.NET types in requests, duplicated `DbContext`/middleware
registration, unsafe error responses, sync I/O. Do not import its caching, Redis, JWT, rate limiting,
file services, or telemetry without a requirement.

## Implementation order

1. Project boundaries, result/error mapping, exception handling, validation behavior, PostgreSQL,
   test harness.
2. `RegisterProject`, `UpdateProject`, `ArchiveProject`, `GetProject`, `ListProjects`.
3. GitHub source/CI adapter behind a port.
4. Health and version probes behind a port.
5. Refresh use case, deterministic version sync, transition-based activity.
6. Dashboard query contract (`GET /api/dashboard/overview`) matching the frontend contract.
7. Swap the frontend mock data source for the real HTTP adapter.

Each step builds and tests before the next begins.
