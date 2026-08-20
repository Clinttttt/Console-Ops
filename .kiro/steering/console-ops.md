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
4. `docs/Console_Ops_Workflows_Context.md` — product authority for the Workflows feature.
5. `AGENTS.md` — shared agent contract, identical intent for every agent (Kiro, Codex, Claude).

If this file ever disagrees with those documents, the documents win and this file must be corrected.
Never let two agents encode different rules: change the docs first, then the steering.

Deep reference lives in on-demand skills, not here:

- `console-ops-backend-slice` — vertical slice + CQRS + result/error recipe.
- `console-ops-frontend` — Angular structure, contracts, and design system.

Remaining work, blockers, and what must not be built yet are tracked in `docs/Console_Ops_Backlog.md`.

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
probes, deterministic version sync, dashboard query, transition-based activity, release history
recorded from GitHub Actions workflow runs, observed availability computed from recorded health checks,
scheduled collection of all of the above, provider-backed application logs, and a configuration status
report. Deployment triggering, restart, rollback, Docker agents, and log ingestion are later phases —
do not build them early.

Logs are **pulled** during the request, the one pass-through provider read in Console Ops: Azure
Container Apps console output through Log Analytics, bounded by window, row cap and timeout. Never build
an `ILoggerProvider`, an ingestion key, or an inbound collector. Framework chatter is excluded by
default at information and below, never a warning or worse, and the count left out is always stated.
An environment on a platform with no reader is reported as unsupported, never offered as a source.

Settings describes **Console Ops itself** — integrations, collection, build — and never repeats project,
environment, deployment or health facts, which have their own screens. Configuration is reported by key
name only: no code path may read a value into a response. `Configured` and `Verified` are different
claims, and only a provider answering earns the second. Nothing on that screen is editable while nothing
there can be persisted at runtime.

Collection is server side and scheduled (`Monitoring:Refresh`), sending the same command as the manual
refresh endpoint so the two can never record different facts. The browser only re-reads stored data; it
never polls a provider, and no screen invents a staleness verdict.

Workflows is the automation workspace: what repository automation exists and how it executed. It is not a second
Deployments screen — Deployments answers which release reached an environment. Classification is honest: a
workflow reads as a deployment only where an operator configured it as an environment's primary deployment
workflow, everything else stays `Unclassified`, and no type, icon, or risk level is inferred from a workflow's
name or trigger. Workflow execution logs belong to Workflows; application logs belong to Logs. A run action
appears only where the provider reports manual dispatch. Starting a workflow additionally requires a risk level an
operator set: unmarked means Console Ops refuses to run it, `normal` asks for an explicit confirmation, and
`destructive` requires the workflow's name to be typed. Risk is never inferred from a name. `docs/Console_Ops_Workflows_Context.md` is the product authority.

Release history records what GitHub proves — a run built a commit and ended a certain way — and links it
to an environment only through that environment's own version observation. Never store or infer which
environment a run deployed to, and never claim a runtime revision until Azure awareness exists.

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
- **Check `git status -sb` before the first commit of a session.** Commit only onto your own
  `agent/<agent>/<task>` branch, created from the integration branch. Never commit onto another
  agent's branch, and never onto `main`: work reaches `main` through a pull request. Kiro's branches
  are `agent/kiro/<task>`.
- Stage explicit file paths, never a directory. Another agent's uncommitted files live in the same
  working tree, and `git add <dir>` has swept them into a commit before.
- Commit only when the user asks.
