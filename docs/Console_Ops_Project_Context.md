# Console Ops — Project Context

## 1. Product Name

**Console Ops**

Console Ops is a personal developer operations control center for monitoring, understanding, and eventually controlling the deployment state of software projects from one place.

The product is primarily intended for a developer managing multiple applications across local and cloud environments. It is not intended to replace GitHub, Azure, Docker, CI/CD platforms, or observability platforms. Instead, Console Ops brings the most useful operational information from those systems into one focused interface.

The core product idea is:

> **One place to understand what is running, where it is running, whether it is healthy, what version is deployed, what changed recently, and whether production matches source control.**

---

## 2. Problem Being Solved

A developer commonly has to jump between several tools just to understand the current state of a project.

For example:

- GitHub to inspect the latest commit.
- GitHub Actions to check whether CI/CD passed.
- Azure Portal to inspect Container Apps, revisions, deployments, and runtime state.
- Docker Desktop or terminal commands to inspect local containers.
- An application health endpoint to confirm the API is responding.
- Application logs to understand failures.
- Environment configuration screens to confirm required settings exist.
- Database dashboards to determine whether infrastructure is reachable.
- Local notes or memory to remember deployment commands, URLs, environment names, and resource names.

This becomes increasingly annoying as the number of projects grows.

Console Ops exists to reduce that operational friction.

Instead of asking:

> “Where is this project deployed, what commit is running, did CI succeed, and why is it unhealthy?”

the developer should be able to open Console Ops and understand the answer immediately.

---

## 3. Primary User

The initial user is a **single developer managing their own projects**.

Console Ops is therefore a personal developer tool first, not a multi-tenant commercial SaaS product.

The product should prioritize:

- personal productivity,
- clarity,
- fast project inspection,
- operational awareness,
- safe deployment controls,
- low setup friction,
- trustworthy information,
- developer-oriented terminology.

Multi-user organizations, enterprise administration, billing, subscriptions, teams, and commercial account management are not priorities for the initial product.

---

## 4. Product Positioning

Console Ops should be understood as a **developer control plane**, not as a hosting platform.

It does not initially:

- build applications itself,
- replace GitHub Actions,
- host containers,
- replace Azure Container Apps,
- replace Docker,
- replace a database provider,
- replace Grafana or Application Insights,
- replace a secrets manager,
- replace Git itself.

Instead, it connects information from those systems.

A simplified mental model is:

```text
Source Control
GitHub
    │
    ▼
CI / Deployment
GitHub Actions
    │
    ▼
Runtime
Azure / Docker / Local
    │
    ▼
Application
Health / Version / Runtime State
    │
    ▼
Console Ops
One operational view
```

Console Ops should answer questions across those boundaries.

---

## 5. Core Product Questions

For every registered project, Console Ops should eventually be able to answer questions such as:

- Is the application currently healthy?
- Is production reachable?
- What environment am I looking at?
- What is the latest source commit?
- What commit is currently deployed?
- Does production match the latest source?
- Did the latest CI/CD run pass?
- When was the last deployment?
- Which deployment/revision is currently active?
- How long is the API taking to respond?
- Is the database dependency healthy?
- Is Redis healthy?
- Are required runtime settings configured?
- What happened recently?
- Was there a restart?
- Did health begin failing after a deployment?
- Is production behind source control?
- Is a local Docker environment currently running?
- Which project currently needs attention?

The dashboard should emphasize answers to these questions rather than merely displaying raw infrastructure data.

---

# 6. Project Registration

A project should be registered once and then monitored from Console Ops.

A project may contain information such as:

- project name,
- short description,
- repository owner,
- repository name,
- default branch,
- repository URL,
- environments,
- production URL,
- staging URL,
- local URL,
- health endpoint,
- version endpoint,
- deployment provider,
- runtime provider,
- Azure resource identifiers,
- Docker/local runtime identifiers,
- expected configuration keys,
- optional links to external services.

Example:

```text
Project
Spinner API

Repository
clint/spinner

Default branch
main

Environment
Production

Runtime
Azure Container Apps

Application URL
https://api.example.com

Health endpoint
/health

Version endpoint
/version
```

Console Ops should store the configuration required to locate and inspect a project.

Runtime truth should still come from the underlying system whenever possible.

---

# 7. Environments

A project may have multiple environments.

Common examples:

- Local
- Development
- Staging
- Production

The environment is an important part of the product.

The user should never have to guess whether information or an action applies to development or production.

Examples:

```text
Spinner API
Production
Healthy
```

```text
AMYL
Local
Running
```

```text
StallTrack
Production
Degraded
```

Production actions should be visually and behaviorally distinct from local actions.

---

# 8. Source Control Awareness

GitHub is the initial source-control integration.

Console Ops should be capable of displaying useful repository state such as:

- repository name,
- default branch,
- latest commit SHA,
- short commit SHA,
- latest commit message,
- commit author,
- commit time,
- current branch,
- latest source revision associated with a deployment.

The most important use case is comparing source with deployed software.

Example:

```text
GitHub main
93fc221

Production
8a17c2f

Production is behind.
```

Or:

```text
GitHub main
93fc221

Production
93fc221

Production is in sync.
```

This comparison is a first-class Console Ops concept.

---

# 9. Version Awareness

Applications managed by Console Ops should preferably expose a safe version endpoint.

Example:

```http
GET /version
```

A response may contain information such as:

```json
{
  "application": "Spinner.Api",
  "version": "1.4.2",
  "commit": "93fc221",
  "environment": "Production",
  "builtAt": "2026-08-13T12:41:00Z"
}
```

The version endpoint must not expose secrets, credentials, connection strings, internal tokens, or sensitive infrastructure information.

Console Ops should use this endpoint to identify what build is actually running.

Important concepts:

- **Source commit** — latest relevant commit in GitHub.
- **Deployed commit** — commit reported by the running application or deployment metadata.
- **In Sync** — deployed commit matches expected source.
- **Behind** — production is running an older commit.
- **Unknown** — deployed version could not be determined.

---

# 10. CI/CD Awareness

GitHub Actions is the initial CI/CD integration.

Console Ops should be capable of displaying:

- latest workflow run,
- workflow name,
- branch,
- associated commit,
- queued state,
- running state,
- success,
- failure,
- cancellation,
- start time,
- completion time,
- duration,
- relevant job/step information where useful.

Examples:

```text
CI
Passed
2m 48s
```

```text
Deploy Production
Failed
Docker push
```

Console Ops should not initially duplicate the CI pipeline implementation.

The CI/CD platform remains responsible for:

- checkout,
- restore,
- build,
- tests,
- Docker build,
- image publishing,
- infrastructure deployment,
- migrations,
- deployment scripts.

Console Ops should observe and eventually trigger those workflows.

---

# 11. Application Health

ASP.NET Core applications may expose health endpoints such as:

```text
/health
```

or separated endpoints such as:

```text
/health/live
/health/ready
```

Console Ops should monitor application health.

Possible high-level states:

- Healthy
- Degraded
- Unhealthy
- Unreachable
- Unknown

Health should never be inferred purely from stored database state when the real application can be checked.

The runtime application or underlying platform is the source of truth.

Console Ops may store the latest health result and historical snapshots for display and analysis.

---

# 12. Dependency Health

The application may include dependency checks in its own health system.

Examples:

- PostgreSQL
- Redis
- queue/broker
- payment provider
- email provider
- object storage
- external API

Console Ops should prefer obtaining dependency status through the application rather than receiving direct credentials for every infrastructure dependency.

Preferred flow:

```text
Console Ops
    │
    ▼
Application /health
    │
    ├── Database
    ├── Redis
    └── Other dependency
```

Example display:

```text
API          Healthy
Database     Healthy
Redis        Healthy
Payments     Degraded
```

This reduces unnecessary credential exposure inside Console Ops.

---

# 13. Health Monitoring History

Console Ops should eventually maintain lightweight health history.

Useful information includes:

- checked time,
- health result,
- response time,
- environment,
- failure reason when safe,
- recovery time,
- uptime percentage,
- average response time,
- recent failures.

Example:

```text
Last 24 hours
99.98% available

Average response
104 ms

Last incident
16 minutes ago
```

The first version does not need to become a full observability system.

Health history should remain focused and useful.

---

# 14. Runtime Awareness

The first important cloud runtime is **Azure Container Apps**.

Console Ops may eventually display:

- Container App name,
- runtime state,
- active revision,
- revision history,
- deployment time,
- replicas where appropriate,
- revision status,
- currently active version,
- traffic/revision information where useful.

Example:

```text
Spinner API

Runtime
Azure Container Apps

Current revision
spinner-api--000021

Commit
93fc221

State
Running
```

Azure remains the source of truth for Azure runtime state.

---

# 15. Local Runtime Awareness

Local projects may use Docker.

Console Ops should eventually be capable of showing local runtime information such as:

```text
AMYL
Local

amyl-api
Running

postgres
Running

redis
Running
```

Potential future local information:

- container state,
- container name,
- port,
- uptime,
- CPU,
- memory,
- logs,
- restart state.

A hosted Console Ops server must not directly expose the developer's Docker socket publicly.

A future local companion/agent may safely provide local runtime information to Console Ops.

The exact implementation of that companion is intentionally outside this project-context document.

---

# 16. Project Status Summary

Console Ops should transform raw signals into a concise human-readable operational state.

Examples:

```text
Spinner

Operational

Production matches main.
Latest CI passed.
All monitored dependencies are healthy.
```

```text
Spinner

Attention Required

Production is 3 commits behind main.
Latest CI passed.
A newer deployment is available.
```

```text
Spinner

Degraded

Latest deployment completed 4 minutes ago.
Database health began failing shortly afterward.
```

The goal is not to use AI to invent explanations.

These summaries should be based on deterministic relationships between known system facts whenever possible.

---

# 17. Drift Detection

Console Ops should eventually detect useful forms of drift.

## Version Drift

Example:

```text
Source
93fc221

Production
8a17c2f

Status
Behind
```

## Configuration Drift

A project may define expected configuration keys.

Example:

```text
Expected

ConnectionStrings__Database
Jwt__Secret
PayMongo__SecretKey
PayMongo__WebhookSecret
Resend__ApiKey
```

Console Ops may compare this expected configuration structure against the target environment.

Example:

```text
Configuration

Database                 Configured
JWT                      Configured
PayMongo                 Configured
Webhook Secret           Missing
Email                    Configured
```

The goal is to know whether configuration exists, not to expose the actual value.

---

# 18. Secrets and Sensitive Information

Console Ops must be conservative with secrets.

It should not become a screen that casually reveals production credentials.

Avoid displaying:

- database passwords,
- connection strings,
- API keys,
- JWT secrets,
- PayMongo secret keys,
- webhook secrets,
- private certificates,
- access tokens,
- cloud client secrets.

A safe configuration display should prefer:

```text
Configured
Missing
Unknown
```

rather than showing values.

Example:

```text
PayMongo Secret
Configured
```

not:

```text
PayMongo Secret
sk_live_xxxxxxxxx
```

Secrets needed by Console Ops itself must be handled securely.

They must never be stored in source control.

---

# 19. Authentication to External Systems

Different providers use different authentication mechanisms.

Console Ops should not assume every provider uses a traditional API key.

Examples:

### GitHub

Initial development may use a fine-grained personal access token.

A more mature integration may use a GitHub App.

### Azure

Azure access should use Microsoft Entra authentication.

When Console Ops is hosted on Azure, managed identity is preferred where practical.

### Docker

Local Docker access is based on access to the Docker Engine, not a normal SaaS API key.

### Application Health / Version

Public read-only health/version endpoints may require no authentication if they expose only safe information.

Private environments may use an authentication mechanism if necessary.

---

# 20. Recent Activity

Console Ops should provide a unified activity stream.

Examples:

```text
09:23
Spinner API deployed
Production · 93fc221
```

```text
09:22
Health check recovered
Spinner API
```

```text
08:41
StallTrack restarted
Production
```

```text
08:39
Database migration completed
StallTrack
```

```text
07:58
AMYL container started
Local
```

Possible event types include:

- deployment queued,
- deployment started,
- deployment succeeded,
- deployment failed,
- workflow cancelled,
- restart requested,
- restart completed,
- health failed,
- health recovered,
- version drift detected,
- configuration drift detected,
- local container started,
- local container stopped.

Activity should be chronological and easy to scan.

---

# 21. Deployment History

Each environment should eventually expose useful deployment history.

Example:

```text
Production Deployments

CURRENT
93fc221
Healthy
Aug 13 · 9:23 PM

PREVIOUS
8a17c2f
Healthy
Aug 12 · 4:14 PM

PREVIOUS
79ce201
Failed health check
Aug 12 · 12:08 PM
```

Important deployment information may include:

- environment,
- commit,
- workflow run,
- deployment/revision ID,
- status,
- started time,
- completion time,
- runtime revision,
- post-deployment health result,
- trigger source.

---

# 22. Deployment Triggering

A later Console Ops version may allow one-click deployment.

Console Ops should preferably trigger an existing deployment workflow rather than reimplement the deployment pipeline.

Conceptually:

```text
Console Ops
    │
    ▼
Trigger existing CI/CD workflow
    │
    ▼
GitHub Actions
    │
    ├── Build
    ├── Test
    ├── Package
    └── Deploy
```

Example user flow:

```text
Deploy Spinner?

Environment
Production

Branch
main

Commit
93fc221

Currently deployed
8a17c2f

3 commits ahead

[Cancel] [Deploy]
```

After deployment starts, Console Ops should show its progress and verify the resulting state.

Example:

```text
Queued
Running
Deployment succeeded
Health check passed
Version verified

Production is in sync.
```

---

# 23. Restart Operations

A future version may allow restart operations.

Examples:

```text
Restart Spinner Production
```

```text
Restart AMYL local API container
```

Production restart actions are considered privileged/destructive operational actions.

They should not behave like casual UI buttons.

They should:

- clearly identify the project,
- clearly identify the environment,
- show what will be affected,
- require intentional confirmation,
- produce an audit event,
- show progress,
- verify health afterward,
- report failure clearly.

---

# 24. Rollback

Rollback is a later-stage feature.

Console Ops should not implement rollback until monitoring, deployment history, environment identification, audit history, and safe operational controls are reliable.

A future rollback workflow may show:

```text
Current
revision-21
93fc221

Previous healthy
revision-20
8a17c2f

Rollback production?
```

After a rollback:

- verify runtime state,
- verify application health,
- verify deployed version,
- record the operation,
- report the final state.

---

# 25. Logs

Console Ops may eventually provide convenient access to application/runtime logs.

The objective is not necessarily to replace full log platforms.

Useful features may include:

- recent logs,
- environment filtering,
- project filtering,
- runtime/source indicator,
- severity,
- timestamps,
- deployment correlation,
- jump to external provider for deeper inspection.

Example:

```text
Spinner / Production

21:42:08 INF Booking created
21:42:09 INF Payment requested
21:42:13 INF Webhook received
21:42:13 INF Payment confirmed
```

---

# 26. Operational Correlation

A major long-term differentiator of Console Ops is correlation.

Individual tools usually know only one part of the system.

Console Ops should understand relationships such as:

```text
Commit
    ↓
CI workflow
    ↓
Deployment
    ↓
Runtime revision
    ↓
Application version
    ↓
Health result
```

This makes higher-value observations possible.

Example:

```text
Deployment 93fc221 completed at 21:40.

Database health began failing at 21:41.

Previous revision was healthy.
```

This is based on timestamps and known relationships.

It does not require generative AI.

---

# 27. Dashboard Purpose

The dashboard is not intended to display every available metric.

Its purpose is rapid operational awareness.

A developer should be able to look at the dashboard for a few seconds and answer:

- Are my projects okay?
- Which environment is affected?
- Is production current?
- Did CI pass?
- Is something failing?
- Did something recently change?
- Which project needs my attention?

The dashboard should prioritize signal over volume.

---

# 28. Dashboard Content

A high-quality dashboard may contain the following major areas.

## Project Overview

Example:

```text
Spinner API
Production
Healthy

main
93fc221

CI
Passed

Production
93fc221

103 ms

In Sync
```

## Deployment / Runtime Flow

A compact visual relationship such as:

```text
GitHub
Source

    →

GitHub Actions
CI/CD

    →

Azure
Runtime

    →

Healthy
```

## Recent Activity

A small chronological stream of important events.

## System State

A structured matrix such as:

```text
                Spinner     StallTrack     AMYL

API             Healthy     Healthy        Running
Database        Healthy     Healthy        Healthy
Redis           Healthy     Healthy        Running
CI              Passed      Passed         N/A
Version Sync    In Sync     In Sync        Local
Configuration   Complete    Complete       Complete
```

The dashboard should remain restrained and readable.

---

# 29. Visual Design Direction

Console Ops should not look like a generic SaaS admin dashboard.

Avoid:

- excessive rounded cards,
- every statistic placed in a separate box,
- giant decorative charts with little value,
- rainbow status colors,
- gradients everywhere,
- glassmorphism,
- oversized icons,
- fake analytics,
- excessive badges,
- excessive shadows,
- visually noisy layouts,
- generic “AI dashboard” aesthetics.

Preferred direction:

- professional,
- restrained,
- developer-oriented,
- calm,
- information-dense without clutter,
- clear typography,
- thin separators,
- strong spacing,
- table/list-based operational surfaces,
- subtle status indicators,
- minimal use of color,
- custom composition instead of card grids.

Suggested visual palette:

- off-white / warm light-gray background,
- charcoal / slate text,
- deep navy as the primary accent,
- green only for positive operational state,
- red only for real failure,
- amber/gold only for warning or limited emphasis,
- muted gray for unknown or inactive states.

Status color should communicate meaning, not decoration.

---

# 30. UX Principles

Console Ops should follow these principles.

## Environment Clarity

Always make the active environment obvious.

A production action must never look indistinguishable from a local action.

## Read Before Control

Monitoring and visibility come before destructive actions.

The earliest versions should be mostly read-only.

## Trustworthy Data

Do not invent state.

Use:

- GitHub as source truth for repository/CI information,
- Azure as source truth for Azure runtime state,
- Docker as source truth for local container state,
- application endpoints as source truth for runtime health/version.

## Progressive Detail

The overview should stay simple.

Detailed logs, workflows, revisions, configuration, and history can live deeper in the project.

## Safe Actions

Deployment, restart, rollback, and similar actions should be intentional and auditable.

## Useful Empty States

If information is not configured or unavailable, say so clearly.

Examples:

```text
Version endpoint not configured
```

```text
No production environment
```

```text
CI integration unavailable
```

Do not fabricate a status.

---

# 31. Suggested Project Screens

The product may eventually include:

## Overview

High-level operational dashboard across all projects.

## Projects

All registered projects and current status.

## Project Detail

Focused view of one project.

Possible sections:

- summary,
- environments,
- source state,
- CI/CD,
- health,
- version synchronization,
- runtime,
- deployments,
- logs,
- configuration,
- recent activity.

## Deployments

Cross-project deployment history.

## Health

Health history and current dependency state.

## Logs

Convenient log access across projects.

## Environments

Environment and runtime target configuration.

## Settings

Integration and Console Ops configuration.

These screens describe product scope only.

Their final technical implementation and structure are intentionally not prescribed here.

---

# 32. Project Detail Example

A project detail page should make the following type of information easy to inspect:

```text
Spinner API
Production

Operational

SOURCE

Repository
clint/spinner

Branch
main

Latest commit
93fc221


CI / CD

Latest workflow
Deploy Production

Status
Passed

Duration
2m 48s


RUNTIME

Provider
Azure Container Apps

Revision
spinner-api--000021

State
Running


APPLICATION

Health
Healthy

Response
103 ms

Version
1.4.2

Commit
93fc221


DEPENDENCIES

Database
Healthy

Redis
Healthy

PayMongo
Healthy


VERSION SYNC

Source
93fc221

Production
93fc221

In Sync
```

The purpose is operational understanding, not decorative statistics.

---

# 33. Alert / Attention Model

Console Ops should eventually distinguish between normal and attention-worthy situations.

Examples:

## Operational

```text
Production matches main.
Latest deployment succeeded.
All monitored dependencies are healthy.
```

## Attention Required

```text
Production is 3 commits behind main.
Latest CI passed.
```

## Degraded

```text
Application is reachable.
Database dependency is unhealthy.
```

## Down

```text
Production health endpoint is unreachable.
```

## Unknown

```text
Runtime status could not be determined.
```

The product should avoid panic-style alerts for minor informational differences.

---

# 34. Notifications

Notifications are not required for the initial version.

Possible later notifications include:

- production became unhealthy,
- production recovered,
- CI failed,
- deployment failed,
- deployment completed,
- version drift detected,
- configuration drift detected.

Console Ops should avoid becoming a noisy notification engine.

Only operationally meaningful events should generate alerts.

---

# 35. V1 — Minimum Useful Product

The first release should be deliberately small.

The goal of V1 is:

> **Register projects, inspect GitHub state, inspect application health/version, and determine whether production matches source control.**

V1 should include:

### Project Management

- register a project,
- edit project configuration,
- remove/archive a project,
- configure repository,
- configure environment,
- configure health URL,
- configure version URL.

### GitHub

- repository connection,
- default branch,
- latest commit,
- latest relevant CI/workflow state.

### Application Monitoring

- health check,
- response time,
- version check,
- deployed commit.

### Version Sync

- compare source commit with deployed commit,
- show In Sync / Behind / Unknown.

### Dashboard

Show each project's:

- name,
- environment,
- health,
- source commit,
- CI status,
- deployed commit,
- version synchronization,
- response time,
- last check.

### Basic Activity

Record useful Console Ops observations such as:

- health failed,
- health recovered,
- version drift detected,
- version synchronized.

### V1 Restrictions

V1 should **not** require:

- one-click deployment,
- restart,
- rollback,
- Docker agent,
- Kubernetes,
- AWS,
- multi-user organizations,
- billing,
- AI,
- full log ingestion,
- full metrics infrastructure.

A read-only V1 is considered a successful first product.

---

# 36. V2 — Cloud Runtime Awareness

After V1 is stable, add deeper Azure Container Apps awareness.

Possible V2 features:

- Azure connection,
- Container App runtime state,
- current revision,
- revision history,
- deployment/revision timestamps,
- replica information where useful,
- association between revisions and commits,
- improved deployment activity.

The dashboard can then answer:

```text
Which Azure revision is running?
```

and:

```text
Which commit belongs to this revision?
```

---

# 37. V3 — Deployment Controls

Add safe GitHub Actions controls.

Possible features:

- trigger deployment workflow,
- display queued/running state,
- cancel workflow,
- rerun failed workflow,
- watch workflow completion,
- verify health afterward,
- verify production version afterward,
- record deployment operation.

The deployment engine should remain the existing CI/CD system.

---

# 38. V4 — Operations and Drift

Possible features:

- Azure restart,
- deployment history,
- health history,
- configuration-presence checking,
- configuration drift detection,
- improved logs,
- correlation between deployments and incidents.

---

# 39. V5 — Local Developer Agent

Add local-machine awareness.

Possible features:

- local companion process,
- Docker container state,
- local API status,
- local environment information,
- local logs,
- safe restart/stop/start operations,
- local runtime metrics.

The local integration must prioritize security.

The Docker Engine should not simply be exposed to the public Internet.

---

# 40. V6 — Advanced Production Controls

Only after monitoring and operational safety are mature:

- rollback,
- revision activation,
- traffic switching,
- production restart,
- deployment comparison,
- post-operation validation,
- expanded audit history.

These actions must be intentionally designed because they can affect production systems.

---

# 41. Non-Goals

Console Ops should **not** become the following during early development:

## A Replacement CI/CD Platform

Do not rebuild GitHub Actions.

## A Cloud Provider

Do not attempt to replace Azure.

## A Container Platform

Do not attempt to become Kubernetes or Docker.

## A Full Observability Platform

Do not rebuild Grafana, Prometheus, Datadog, Application Insights, or Sentry.

Console Ops may surface selected useful signals from such systems later.

## A Secrets Vault

Do not make Console Ops responsible for exposing and manually managing every application secret.

## An AI Product

AI is not required for the core value proposition.

The product is useful through deterministic operational data.

## A Generic Enterprise SaaS

Teams, organizations, billing, subscription plans, RBAC matrices, tenant management, and enterprise administration are not initial priorities.

---

# 42. Key Product Differentiator

Console Ops is valuable because it understands the relationship between separate developer systems.

A normal deployment may involve:

```text
GitHub Commit
    ↓
GitHub Actions
    ↓
Container Image
    ↓
Azure Revision
    ↓
Running Application
    ↓
Health Result
```

GitHub understands source and CI.

Azure understands runtime resources.

The application understands its own health.

Console Ops should connect those facts.

This allows Console Ops to say things such as:

```text
Production is healthy and matches main.
```

or:

```text
Production is healthy but 3 commits behind main.
```

or:

```text
Deployment succeeded, but the new revision failed its health check.
```

or:

```text
Database health began failing shortly after the latest deployment.
```

That cross-system understanding is more important than simply displaying raw dashboards from each provider.

---

# 43. Product Personality

Console Ops should feel like a serious developer utility.

Desired qualities:

- calm,
- precise,
- capable,
- technical,
- trustworthy,
- restrained,
- fast,
- transparent.

Avoid gimmicky terminology.

Prefer professional terms such as:

- Projects
- Environments
- Deployments
- Runtime
- Health
- Source
- Configuration
- Revisions
- Activity
- Operations
- Logs
- Version Sync

Avoid unnecessary gaming language, overly playful wording, or novelty terminology in core operational areas.

---

# 44. Naming Guidance

The official project/product name for this context is:

# **Console Ops**

Suggested short description:

> **A personal control center for project deployments, runtime health, and environment state.**

Alternative product description:

> **Console Ops brings source control, CI/CD, runtime health, deployment state, and local environments into one operational view.**

Internal language should consistently use **Console Ops** rather than earlier working names.

---

# 45. Example Initial Projects

The product should be capable of registering very different projects.

Example:

```text
Spinner API
Production
Azure Container Apps
GitHub Actions
ASP.NET Core
```

```text
StallTrack
Production
Azure
GitHub Actions
ASP.NET Core
```

```text
AMYL
Local
Docker
ASP.NET Core / local services
```

Console Ops should not assume every project has every integration.

For example:

- a local-only project may have no production deployment,
- a simple API may have no Redis,
- a project may not expose a version endpoint yet,
- a project may not use Docker,
- a project may not use Azure.

Unsupported or unconfigured data should display honestly as unavailable rather than being fabricated.

---

# 46. Example Dashboard State

A possible concise operational dashboard might communicate:

```text
CONSOLE OPS

Spinner API
Production
Healthy

Source
main · 93fc221

CI
Passed

Runtime
Azure · revision 21

Production
93fc221

Response
103 ms

In Sync


StallTrack
Production
Healthy

Source
main · 72ca192

CI
Passed

Runtime
Azure · revision 18

Production
72ca192

Response
91 ms

In Sync


AMYL
Local
Running

Source
develop · 19cf281

Runtime
Docker

Production
Not configured
```

This is intentionally more useful than showing dozens of unrelated metrics.

---

# 47. Success Criteria

Console Ops is successful when the developer starts opening it naturally during development and deployment work.

Signs of success:

- the developer no longer has to remember every project URL,
- deployment state is visible immediately,
- source-vs-production drift is obvious,
- CI failures are visible without opening GitHub first,
- production health is visible without manually testing endpoints,
- project runtime information is centralized,
- recent operational changes are easy to understand,
- deployment troubleshooting requires fewer tabs and terminal commands,
- local and production environments are easier to reason about,
- the developer trusts Console Ops as the first place to inspect project state.

The strongest success metric is practical:

> **Console Ops should save the developer time every week.**

---

# 48. Guidance for the Coding Agent

When implementing Console Ops, treat this document as **product context and behavioral intent**, not as a required software architecture.

Do not infer that any specific folder structure, design pattern, architectural style, project layout, CQRS approach, repository pattern, or infrastructure organization is mandated by this document.

The developer has their own architecture conventions and local architecture references.

Before making architecture-level assumptions, use the architecture guidance provided separately by the developer.

When product behavior conflicts with implementation convenience, preserve the product behavior described here unless the developer explicitly changes the requirement.

Prioritize:

1. trustworthy state,
2. clear environment boundaries,
3. safe handling of credentials,
4. useful operational information,
5. a restrained professional interface,
6. small incremental releases,
7. read-only monitoring before destructive controls.

---

# 49. One-Sentence Product Definition

> **Console Ops is a personal developer operations control center that unifies project source state, CI/CD status, deployed version, runtime health, environment configuration, deployment activity, and eventually safe operational controls in one place.**
