---
inclusion: always
---

# Console Ops — always-on rules

Console Ops is a personal developer operations control center: one place to see source state,
CI/CD status, deployed version, runtime health, environment configuration, and activity.

## Authority order

1. `docs/Console_Ops_Project_Context.md` — product behavior and scope (what/why).
2. `docs/Console_Ops_Architecture.md` — implementation rules and boundaries (how).
3. `docs/Console_Ops_V1_API_Contract.md` — approved V1 transport and integration semantics.
4. `AGENTS.md` — shared agent contract, identical intent for every agent (Kiro, Codex, Claude).

If this file ever disagrees with those documents, the documents win and this file must be corrected.
Never let two agents encode different rules: change the docs first, then the steering.

Deep reference lives in on-demand skills, not here:

- `console-ops-backend-slice` — vertical slice + CQRS + result/error recipe.
- `console-ops-frontend` — Angular structure, contracts, and design system.

## Non-negotiables

- Modular monolith. Vertical Slice Architecture, CQRS via MediatR, focused DDD. One end-to-end use
  case at a time.
- Dependency direction: `Domain` <- `Application` <- `Infrastructure` <- `Api`. Domain and
  Application never reference ASP.NET Core, EF Core provider types, or provider SDKs.
- Banned until a concrete requirement exists: generic repository/unit-of-work, event sourcing,
  microservices, message broker, NgRx, Redis, multi-tenancy, enterprise RBAC, AI features.
- Expected failures return `Result`/`Result<T>` with stable codes (`Projects.NotFound`). Exceptions
  are for unexpected faults only. HTTP status codes exist only at the API boundary (RFC 7807).
- Provider DTOs stay inside their Infrastructure adapter and are mapped to application-owned models.
- `CancellationToken` flows through endpoint -> MediatR -> EF Core -> outbound HTTP. Timestamps are
  UTC; inject `TimeProvider` where time affects behavior.
- Name the domain concept `ProjectEnvironment`, never `Environment`.

## Truthfulness of operational state

- The runtime is the source of truth: GitHub for source/CI, Azure for Azure runtime, Docker for local
  containers, the application's own endpoints for health/version. A database row is a timestamped
  observation, not proof.
- Version sync is deterministic: `InSync` on normalized commit match, `Behind` only with known
  ancestry/order, otherwise `Unknown`.
- Say `Unknown`, `Not configured`, or `Unavailable`. Never fabricate or pad a status, metric, or
  explanation.

## Security

- Never print, log, commit, or document connection strings, tokens, keys, or secrets. Inspect config
  by key name and display only `Configured` / `Missing` / `Unknown`.
- Credentials come from user-secrets, environment variables, or a secret store.
- Flag any network-exposed endpoint added without authentication.

## Product phase discipline

V1 is read-only: register/edit/archive projects, GitHub source + workflow state, health and version
probes, deterministic version sync, dashboard query, transition-based activity. Deployment
triggering, restart, rollback, Docker agents, and log ingestion are later phases — do not build them
early.

## CI provider

GitHub Actions is the initial CI/CD provider everywhere in code, contracts, and UI. Azure DevOps
appears only in the reference mockup and must not be implemented.

## Naming and voice

Use "Console Ops". Never `DevDeck` or any earlier working name. Use professional operational
vocabulary: Projects, Environments, Deployments, Runtime, Health, Source, Configuration, Revisions,
Activity, Version Sync.

## Working agreement

- Read the two docs before architecture or feature work.
- Prefer one complete tested slice over empty scaffolding. Every abstraction needs a domain meaning,
  an external-boundary test seam, or more than one implementation.
- Build/test after each change; add a regression test for every bug fix.
- Concurrent agents use one Git worktree + task branch each, outside `C:\dev\ConsoleOps`. Sequential
  or read-only work stays in the primary worktree. Only one agent owns EF Core migrations per batch.
- Commit only when the user asks.
