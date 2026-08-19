# Console Ops — Workflows Feature Context

## 1. Feature Name

**Workflows**

The Workflows feature is the automation workspace of Console Ops.

Its purpose is to discover, present, and operate repository automation in a way that is useful inside Console Ops without turning the product into a copy of GitHub Actions.

The first provider is GitHub Actions, but the product concept should remain provider-neutral.

A concise definition:

> **Workflows is the Console Ops workspace for discovering repository automation, inspecting workflow runs, understanding run outcomes, viewing jobs/steps/artifacts, and safely triggering supported workflows.**

---

## 2. Why This Feature Exists

Console Ops originally focused on deployment awareness.

A connected repository may contain far more automation than a single deployment workflow.

Examples:

```text
CI
Deploy production
Database backup
Database restore
Backup freshness
Drop a rehearsal database
Publish Collector APK
Security scan
Release package
```

Only some of these are deployments.

If Console Ops shows all of them inside the Deployments page, the meaning of “deployment” becomes ambiguous.

Therefore:

```text
Deployments
→ release/deployment history

Workflows
→ repository automation
```

This separation is intentional.

---

## 3. Core Product Question

The Workflows page should answer:

> **“What automation exists for this project, what happened when it ran, and can I safely run it from Console Ops?”**

It should also help answer:

- What workflows exist in this repository?
- Which workflow is the primary deployment workflow?
- Which workflow ran most recently?
- Did the latest run pass or fail?
- What branch and commit did it use?
- What triggered the run?
- How long did it take?
- What jobs ran?
- Which job or step failed?
- Did the run publish an artifact?
- Can this workflow be manually triggered?
- What inputs does it require?
- Is the workflow destructive or high-risk?
- Where can I inspect the corresponding GitHub run?

---

## 4. Relationship to Existing Console Ops Pages

The page responsibilities must remain distinct.

```text
Overview
→ What needs my attention overall?

Projects
→ What applications do I manage?

Environments
→ Where do those applications run?

Deployments
→ What release moved into an environment?

Workflows
→ What repository automation exists and how did it execute?

Health
→ Is the application and its dependencies functioning?

Logs
→ What happened inside the running application/runtime?

Settings
→ How does Console Ops itself connect and operate?
```

The Workflows page must not become another deployment-history page.

The Deployments page must not become a generic workflow browser.

---

## 5. Important Concept: Deployment Workflow vs All Workflows

A repository can expose many workflows.

Console Ops should discover the complete workflow inventory.

Example:

```text
Repository
EEMO-Cantilan-SDS

Detected workflows

- Backup freshness
- CI
- Database backup
- Database restore
- Deploy production
- Drop a rehearsal database
- Publish Collector APK
```

However, one environment may designate one workflow as its **primary deployment workflow**.

Example:

```text
Production
└── Primary deployment workflow
    └── Deploy production
```

The product rule is:

> **A repository can have many workflows, but an environment has zero or one primary deployment workflow.**

Zero is valid.

Example:

```text
Local
Deployment workflow
Not configured
```

---

## 6. Why Multiple Deployment Workflows Are Not Selected During Setup

Console Ops needs one authoritative answer to:

> “Which workflow represents a deployment of this environment?”

If multiple workflows are selected as “deployment workflows,” the product becomes ambiguous.

Example ambiguity:

```text
CI
Deploy production
Database backup
```

If all three were treated as deployments:

- Which one should the Deploy button trigger?
- Which run means production was deployed?
- Does a backup create a deployment record?
- What if CI passes but Deploy production fails?

Therefore, the Add Project / Add Environment flow should select zero or one primary deployment workflow.

The other workflows remain available under Workflows.

---

## 7. Workflows Page — Primary Purpose

The main page should present a repository automation inventory.

It should not be a giant spreadsheet.

It should not present unrelated dashboards.

The visual direction should be:

- restrained,
- developer-focused,
- operational,
- low color,
- minimal badges,
- clear hierarchy,
- list/ledger style,
- workflow-centered,
- not generic SaaS cards.

A conceptual layout:

```text
Workflows

Automation inventory and recent run activity across connected repositories.


Search workflows...

All   Deployment   CI   Maintenance   Release

All Projects


WORKFLOW INVENTORY

EEMO-Cantilan-SDS
6 workflows

Deploy production
Primary deployment · Production
In progress · 8 min ago
master · 2ac8bf0 · push

Runs   Run logs   Run


CI
Passed · 2 hr ago
master · 4b09f80 · push

Runs   Run logs


Database backup
Passed · 5 hr ago
master · 89ad1c2 · schedule

Runs   Run logs   Run
```

---

## 8. Workflow Discovery

When a project is connected to GitHub, Console Ops should discover repository workflows automatically.

The user should not manually type workflow file names when the provider can supply them.

Console Ops may display discovered facts such as:

- workflow name,
- workflow path,
- workflow provider identity,
- active/disabled state,
- latest run,
- latest run status,
- latest run conclusion,
- branch,
- commit,
- actor,
- trigger/event,
- started time,
- completed time,
- duration,
- ability to dispatch manually when known.

Examples:

```text
CI
.github/workflows/ci.yml
```

```text
Deploy production
.github/workflows/deploy-production.yml
```

The provider remains the source of truth for workflow existence and run state.

---

## 9. Provider-Neutral Product Language

The page should be named:

# **Workflows**

Do not name the Console Ops domain feature:

```text
GitHub Actions
```

GitHub Actions is the first provider.

Console Ops should use generic product language where possible.

Conceptually:

```text
Console Ops Workflow
    │
    ├── GitHub Actions workflow
    ├── future Azure DevOps pipeline
    ├── future AWS pipeline
    └── future provider automation
```

Do not invent support for other providers before they exist.

The product terminology should simply avoid making GitHub part of the feature name.

---

## 10. Workflow Types / Classification

GitHub does not provide a reliable semantic business category such as:

```text
CI
Deployment
Maintenance
Release
Backup
```

Therefore, Console Ops must not pretend it always knows what a workflow means.

Use a conservative classification model.

Possible Console Ops workflow types:

```text
Unclassified
Deployment
CI
Maintenance
Release
```

Optional future types may be added only when there is a real product need.

---

## 11. Classification Rules

### Explicit Deployment Classification

If the user selected a workflow as an environment's primary deployment workflow:

```text
Deploy production
```

Console Ops may confidently classify it as:

```text
Deployment
```

because the developer explicitly gave it that meaning.

### Other Workflows

Other discovered workflows should initially remain:

```text
Unclassified
```

unless:

- the developer explicitly classifies them, or
- Console Ops has another authoritative configuration source.

Do not silently decide:

```text
Database backup = Maintenance
```

only because the name contains “backup.”

---

## 12. Suggested Classification

A future version may suggest a type.

Example:

```text
Database backup

Suggested type
Maintenance

Accept
Change
```

Suggestions may consider:

- workflow name,
- path,
- trigger types,
- job names,
- step names.

However:

> **Suggestion must not be treated as authoritative classification without confirmation.**

Avoid false certainty.

---

## 13. Generic Workflow Icon Strategy

The Workflows page should not use arbitrary semantic icons unless Console Ops truly knows the workflow type.

For V1:

- use one consistent generic workflow icon for unclassified workflows,
- optionally use a specific deployment icon for explicitly configured deployment workflows.

Do not automatically show:

```text
database icon
rocket icon
package icon
```

based only on workflow names.

This can make the UI look clever while being semantically wrong.

A future icon system may use a shared Console Ops workflow glyph with small interior variations for known classifications.

Example concept:

```text
generic workflow       → same base glyph
deployment             → base glyph + deploy symbol
CI                     → base glyph + code/check symbol
maintenance            → base glyph + maintenance symbol
release                → base glyph + package/artifact symbol
```

The visual system should remain consistent with Console Ops.

---

## 14. Selected Workflow Detail

Selecting a workflow should open a focused detail surface.

Example:

```text
SELECTED WORKFLOW

Deploy production

Repository
EEMO-Cantilan-SDS

Type
Deployment

Path
.github/workflows/deploy-production.yml

Latest run
#535

Status
In progress

Branch
master

Commit
2ac8bf0

Trigger
push

Actor
Clinttttt

Started
Aug 18, 09:05

Supports manual run
Yes
```

Do not repeat unrelated project/environment details.

---

## 15. Latest Run Overview

The selected workflow detail may show a compact job summary.

Example:

```text
LATEST RUN OVERVIEW

Prepare
Completed
1m 12s

Deploy
In progress

Verify
Pending
```

This is useful because it answers:

> “Where is the workflow currently stuck?”

The main workflow inventory should not show every job/step.

That detail belongs after selection or in the run detail screen.

---

## 16. Workflow Runs

Each workflow should expose run history.

Example:

```text
CI

RUN HISTORY

#938
Passed
master
2ac8bf0
4m 18s
8 min ago

#937
Passed
master
d713a12
4m 02s
2 hr ago

#936
Failed
feature/foo
a82cef1
1m 51s
Yesterday
```

Useful fields include:

- run number,
- status,
- conclusion,
- branch,
- commit,
- trigger,
- actor,
- started time,
- completed time,
- duration.

Avoid showing fields that do not help operationally.

---

## 17. Run Detail

Clicking a run should open a workflow execution view.

Example:

```text
CI
Run #938

Passed

Repository
EEMO-Cantilan-SDS

Branch
master

Commit
2ac8bf0

Trigger
push

Actor
Clinttttt

Started
17:02

Completed
17:06

Duration
4m 18s
```

Then:

```text
JOBS

Backend
Passed
2m 41s

Frontend
Passed
1m 37s
```

---

## 18. Job Detail

A job may show its steps.

Example:

```text
Backend

Passed
2m 41s

STEPS

Checkout
Passed

Setup .NET
Passed

Restore
Passed

Build
Passed

Unit tests
Passed

Integration tests
Passed
```

This gives Console Ops a native workflow inspection experience.

Do not attempt to completely clone GitHub Actions.

Console Ops should focus on operational clarity.

---

## 19. Workflow Logs vs Application Logs

This boundary is critical.

### Workflow Logs

These belong to Workflows.

Examples:

```text
dotnet restore
dotnet test
docker build
az containerapp update
npm ci
```

These are CI/CD execution logs.

### Application Logs

These belong to Logs.

Examples:

```text
Order created
Payment timeout
Database query failed
Application started
HTTP request completed
```

The user should not have to guess which page contains which kind of log.

---

## 20. “Logs” Action on Workflows

A workflow or run may provide:

```text
View run logs
```

This action should inspect the execution logs of that GitHub workflow run.

It should not redirect to Console Ops application/runtime Logs.

Use clear labeling to avoid ambiguity.

---

## 21. Manual Workflow Triggering

Some workflows support manual execution.

Console Ops should expose a Run action only when the provider indicates that manual dispatch is supported.

Do not show:

```text
Run workflow
```

for every workflow by default.

Possible states:

```text
Manual run supported
Manual run unavailable
Unknown
```

---

## 22. Manual Run Flow

For a workflow with no inputs:

```text
Run CI?

Branch
master

Run the selected workflow?

Cancel
Run
```

For workflows with inputs:

```text
Database restore

Target environment
Rehearsal

Backup
2026-08-18 02:00

Cancel
Run workflow
```

Console Ops should present workflow inputs cleanly.

Do not invent inputs that the provider/workflow does not define.

---

## 23. Safety for Destructive Workflows

Not all workflows are equal.

Examples of low-risk actions:

```text
CI
Backup freshness
Build artifact
```

Examples of high-risk/destructive actions:

```text
Database restore
Drop a rehearsal database
Production deployment
Potential data migration
```

Console Ops should support a safety classification for manual execution behavior.

The product must not make a destructive workflow feel like a casual button.

Possible confirmation pattern:

```text
DROP REHEARSAL DATABASE

Target
rehearsal-20260818

This workflow performs a destructive operation.

Type the target name to confirm:

rehearsal-20260818

Cancel
Run workflow
```

The exact implementation may evolve.

The behavior requirement is:

> **Higher-risk operations require stronger intentional confirmation.**

---

## 24. Trigger Source

Run history should make the trigger visible where useful.

Examples:

```text
push
pull_request
schedule
workflow_dispatch
workflow_call
```

Console Ops may present friendly descriptions:

```text
Push
Manual
Scheduled
Reusable workflow
```

but preserve the real provider fact underneath.

Do not infer workflow purpose from trigger type.

Example:

```text
push
```

does not automatically mean CI.

A deployment can also run on push.

---

## 25. Artifacts

Some workflows produce artifacts.

Examples:

```text
Collector.apk
database-backup.dump
coverage-report.zip
published-package.zip
```

Console Ops should eventually surface run artifacts when available.

Example:

```text
Publish Collector APK

LATEST ARTIFACT

Collector.apk

Created
Aug 18, 14:25

Size
72.6 MB

Download
```

Artifact information should come from the provider.

Do not invent:

- semantic version,
- retention period,
- deployment target,

unless Console Ops can actually derive those facts.

---

## 26. Deployments and Workflows Cross-Link

The same GitHub Actions run may appear in two Console Ops contexts.

Example:

```text
deploy-production.yml #535
```

### Deployments Perspective

The user asks:

```text
Did commit 2ac8bf0 become Production?
Which environment reports it?
Was the deployed version verified?
```

### Workflows Perspective

The user asks:

```text
How did workflow run #535 execute?
Which jobs ran?
Which step failed?
What logs/artifacts exist?
```

These are two views over related provider facts.

Console Ops should cross-link rather than duplicate product concepts.

Example:

```text
Deployments
→ Workflow
→ Workflows / Deploy production / Run #535
```

---

## 27. Deployment Workflow Indicator

A workflow explicitly configured as an environment's deployment workflow may display:

```text
Primary deployment · Production
```

This is useful product context.

Example:

```text
Deploy production

Primary deployment · Production
```

Do not show “deployment” labels for workflows merely because the name sounds like deployment.

---

## 28. Project Grouping

The Workflows page should make repository/project ownership obvious.

Example:

```text
EEMO-Cantilan-SDS
6 workflows
```

then the workflows beneath it.

For multiple projects, the page may group by project or use a project filter.

The page should not become visually overwhelming when multiple repositories are connected.

---

## 29. Search and Filters

Useful V1 filters:

- project,
- workflow search,
- classification,
- state/status if needed.

Example:

```text
Search workflows...

All
Deployment
CI
Maintenance
Release

All Projects
```

Do not create a large advanced-filter panel in V1.

---

## 30. Status Language

Workflow status should reflect provider truth.

Useful concepts include:

```text
Queued
In progress
Completed
Waiting
```

Conclusions may include:

```text
Passed
Failed
Cancelled
Skipped
Timed out
Action required
Neutral
```

The UI should avoid collapsing everything into only:

```text
Passed / Failed
```

when the provider exposes an active status.

---

## 31. Color Usage

Workflows must remain visually restrained.

Use color primarily for status meaning.

Examples:

```text
green
→ success

blue
→ running/in progress

red
→ failure

gray
→ waiting/unknown/disabled

amber
→ warning/action-required when genuinely applicable
```

Do not use different bright colors for every workflow category.

Category is primarily communicated through text/iconography, not color.

---

## 32. Disabled Workflows

If GitHub reports a disabled workflow, Console Ops should display it honestly.

Example:

```text
Security scan

Disabled
```

Do not hide it automatically unless the user filters it out.

Disabled workflow behavior should not be confused with:

```text
Failed
```

---

## 33. Repository Refresh

Workflow inventory should reflect provider state.

If a workflow is added, renamed, disabled, or removed in GitHub:

```text
GitHub
    ↓
Console Ops refresh
    ↓
Workflow inventory updates
```

Console Ops may retain historical run references where product history requires it, but current workflow existence comes from the provider.

---

## 34. Empty States

If a connected repository has no GitHub Actions workflows:

```text
No workflows found.

This repository does not currently expose GitHub Actions workflows.
```

Do not fabricate examples.

If the GitHub integration cannot read workflows:

```text
Workflow inventory unavailable.

Console Ops could not read repository workflows.
```

Expose the reason when safe and useful.

---

## 35. Manual Run Availability

A workflow row/detail may show:

```text
Run
```

only when appropriate.

Examples:

```text
CI
Runs
Run logs
```

if manual dispatch is not supported.

```text
Database backup
Runs
Run logs
Run
```

if manual dispatch is supported.

---

## 36. Workflow Inputs

When a workflow requires dispatch inputs, Console Ops may render them from the provider/workflow definition.

Example:

```text
Run Database restore

Environment
[ Rehearsal ]

Backup
[ 2026-08-18T02:00 ]

Confirm
```

Inputs must preserve:

- required vs optional,
- allowed options where defined,
- default values where defined.

Do not create a separate custom Console Ops form schema when the workflow definition is already authoritative.

---

## 37. Branch / Ref Selection

A manual workflow run may require a ref.

Console Ops should use a sensible default such as the configured/default source branch when valid, while allowing change if needed.

Example:

```text
Branch
master

Change
```

Do not silently run against an unexpected branch.

---

## 38. Run Progress

When Console Ops starts a workflow:

```text
Queued
    ↓
In progress
    ↓
Completed
```

The UI should follow the resulting run.

Example:

```text
Deploy production

Running

Prepare
Passed

Deploy
In progress

Verify
Waiting
```

Console Ops should not report success merely because the dispatch request was accepted.

Success means the workflow run itself completed successfully.

---

## 39. Audit / Activity

Manual runs initiated from Console Ops should eventually be visible in activity/audit context.

Useful facts:

```text
Workflow
Database backup

Requested by
Local operator

Requested at
22:41

Provider run
#542
```

The Workflows page may show this in run detail.

Do not build a duplicate activity feed if the product already has an appropriate activity surface.

---

## 40. V1 Scope

The first useful Workflows version should be primarily read-oriented.

V1 should include:

### Workflow Inventory

- discover repository workflows,
- show workflow name,
- show workflow path where useful,
- show workflow active/disabled state,
- show latest run summary,
- show project/repository ownership,
- identify explicitly configured deployment workflow.

### Latest Run

- status/conclusion,
- branch,
- commit,
- trigger,
- actor,
- timestamps,
- duration.

### Run History

- list recent runs,
- inspect individual run.

### Jobs

- list jobs for a run,
- show status/conclusion/duration.

### Steps

- show step names and outcomes where available.

### External Link

- open the underlying provider workflow/run in GitHub.

### Classification

- Deployment when explicitly configured,
- otherwise Unclassified by default.

---

## 41. V1 Non-Goals

Do not require the following for the first release:

- automatic semantic classification,
- manual workflow triggering,
- destructive workflow controls,
- artifacts,
- workflow log rendering,
- rerun/cancel,
- workflow editing,
- YAML editing,
- workflow creation,
- multi-provider workflow support,
- workflow analytics,
- AI workflow summaries.

The Workflows page is useful even as a read-only inventory + execution history feature.

---

## 42. V2 — Manual Execution

After read-only behavior is stable:

- detect manual dispatch support,
- show Run action only when supported,
- render workflow inputs,
- choose branch/ref,
- dispatch workflow,
- follow run progress,
- show completion result,
- record who initiated it.

---

## 43. V3 — Safety and Operations

Add stronger controls:

- destructive workflow confirmation,
- workflow risk metadata,
- rerun failed workflow,
- cancel running workflow,
- improved operation auditing.

Risk must be explicit/configured.

Do not infer destructive behavior solely from workflow names.

---

## 44. V4 — Artifacts

Add provider-backed artifacts:

- list run artifacts,
- artifact metadata,
- download action,
- artifact expiration/availability if provided.

Possible useful workflows:

```text
Publish Collector APK
Database backup
Release package
Coverage report
```

---

## 45. V5 — Richer Classification

Optional later functionality:

- user-defined workflow type,
- suggested type,
- known deployment mapping,
- custom display label,
- workflow grouping.

Suggestions must remain suggestions.

---

## 46. Workflow Classification Example for Current Repository

For the current repository, Console Ops may eventually represent:

```text
Deploy production
Type: Deployment
Reason: Explicitly configured as Production deployment workflow

CI
Type: Unclassified
Suggested: CI

Database backup
Type: Unclassified
Suggested: Maintenance

Database restore
Type: Unclassified
Suggested: Maintenance

Backup freshness
Type: Unclassified
Suggested: Maintenance

Drop a rehearsal database
Type: Unclassified
Suggested: Maintenance

Publish Collector APK
Type: Unclassified
Suggested: Release
```

Only the explicit Deployment classification is authoritative until the user accepts other classifications.

---

## 47. Example Workflows Main Page

A possible product presentation:

```text
WORKFLOWS

EEMO-Cantilan-SDS
7 workflows


Deploy production
Primary deployment · Production

In progress
8 min ago

master · 2ac8bf0 · push

Runs    Run logs    Run


CI

Passed
2 hr ago

master · 4b09f80 · push

Runs    Run logs


Database backup

Passed
5 hr ago

master · 89ad1c2 · schedule

Runs    Run logs    Run


Database restore

No recent run

Manual

Runs    Run


Backup freshness

Passed
6 hr ago

master · d3f7a91 · schedule

Runs    Run logs


Drop a rehearsal database

Passed
4 days ago

master · 1d92cab · manual

Runs    Run logs    Run


Publish Collector APK

Passed
1 day ago

master · f8e12d4 · push

Runs    Run logs
```

This example is illustrative.

Real UI must only show provider-backed facts.

---

## 48. Example Selected Workflow

```text
SELECTED WORKFLOW

Deploy production

Primary deployment · Production

Status
In progress

Repository
EEMO-Cantilan-SDS

Path
.github/workflows/deploy-production.yml

Latest run
#535

Branch
master

Commit
2ac8bf0

Trigger
push

Actor
Clinttttt

Started
Aug 18, 09:05

Manual run
Supported


LATEST RUN OVERVIEW

Prepare
Passed
1m 12s

Deploy
In progress

Verify
Waiting


View runs
Run workflow
```

---

## 49. UX Principles

### Provider Truth

Never invent workflow facts.

GitHub is the source of truth for:

- workflow existence,
- run state,
- job state,
- step state,
- artifacts,
- trigger,
- actor,
- timestamps.

### Semantic Honesty

Do not claim Console Ops knows a workflow's purpose unless:

- the user explicitly configured it,
- the user accepted a classification,
- another authoritative source exists.

### Progressive Detail

Main page:

```text
workflow + latest state
```

Run page:

```text
run details
```

Job page:

```text
jobs and steps
```

Avoid placing everything in one view.

### Safe Controls

Read first.

Control later.

Destructive operations require intentional behavior.

### Minimal Duplication

Cross-link existing Console Ops surfaces instead of duplicating them.

---

## 50. Visual Design Guidance

Workflows should remain consistent with existing Console Ops pages.

Use:

- white/off-white background,
- deep navy text,
- thin separators,
- restrained borders,
- minimal shadows,
- consistent project/workflow icons,
- small semantic state indicators,
- generous but efficient spacing.

Avoid:

- rainbow category colors,
- giant cards,
- gradient workflow tiles,
- excessive status badges,
- decorative charts,
- GitHub-like UI cloning,
- oversized metrics,
- AI-style dashboard patterns.

The main workflow inventory may use flat stacked rows rather than a classic column-heavy table.

---

## 51. Product Value

Workflows should make Console Ops useful beyond deployment monitoring.

Without Workflows:

```text
Console Ops
→ deployment and runtime viewer
```

With Workflows:

```text
Console Ops
→ operational control center for project automation
```

It becomes possible to inspect:

```text
CI
Backups
Restores
Builds
Deployments
Artifact publication
Maintenance
```

from one product while preserving clear domain boundaries.

---

## 52. Success Criteria

The feature is successful when:

- the developer no longer needs to open GitHub Actions just to discover what workflows exist,
- latest workflow health is visible quickly,
- failed jobs/steps are easy to identify,
- deployment workflows link naturally to Deployments,
- workflow execution logs remain distinct from application Logs,
- manually runnable workflows can eventually be triggered safely,
- artifacts can eventually be retrieved without unnecessary navigation,
- Console Ops does not pretend to understand workflow semantics it cannot prove.

---

## 53. Guidance for the Coding Agent

Treat this document as **product and behavioral context**.

It intentionally does not prescribe:

- project architecture,
- folder structure,
- domain-layer organization,
- CQRS usage,
- repository pattern,
- persistence design,
- API route design,
- polling/webhook implementation,
- provider abstraction details,
- caching strategy.

The developer has separate architecture references and local conventions.

Use those references for implementation structure.

When deciding behavior, preserve the following product rules:

1. Workflows and Deployments are different concepts.
2. All repository workflows may be discovered.
3. Each environment has zero or one primary deployment workflow.
4. Deployment classification is authoritative only when explicitly configured.
5. Other workflow classifications default to Unclassified.
6. Do not infer semantic type as fact from name/trigger.
7. Workflow execution logs belong to Workflows, not application Logs.
8. Manual Run only appears when genuinely supported.
9. Destructive operations require stronger confirmation.
10. Provider state is the source of truth.
11. V1 should favor read-only discovery/history before control.
12. Do not fabricate provider facts for a richer UI.

---

## 54. One-Sentence Feature Definition

> **Workflows is the Console Ops automation workspace that discovers repository workflows, shows their latest and historical execution state, exposes jobs/steps and eventually artifacts/manual execution, while keeping deployment semantics, application logs, and workflow classification honest and distinct.**
