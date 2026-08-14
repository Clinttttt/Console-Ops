# Console Ops — Add Project import plan

## Status

Design plan, agreed 2026-08-14. Subordinate to `Console_Ops_Project_Context.md`,
`Console_Ops_Architecture.md`, and `Console_Ops_V1_API_Contract.md`. If this plan ever disagrees with
those documents, they win and this plan is corrected.

Phase 0 is implemented. **Phases 1 and 2 are implemented end to end** and live at
`GET /api/github/repositories` and `GET /api/github/repositories/{owner}/{repository}/workflows`.
Phases 3 and 4 are not built and must not be faked in the UI before their endpoint exists.

### Phases 1 and 2 as built

- Application: `IGitHubRepositoryCatalog` port with catalog read models, and two query slices under
  `Features/GitHub` returning `Result<T>` with stable `GitHub.*` error codes.
- Infrastructure: `GitHubRepositoryCatalog` on its own typed `HttpClient`. Both GitHub clients are
  configured by one `ConfigureGitHubClient` method so base address, timeout, and credential handling
  cannot drift between adapters. Provider DTOs stay inside the adapter.
- Repositories come from `user/repos` sorted by push time, filtered on owner and name, capped at 30 with
  `hasMore` from GitHub's `Link` header. Workflows come from the Actions API, each with its latest run
  outcome; a run that cannot be read leaves the workflow listed with an `unknown` conclusion rather than
  dropping it.
- Failures map to `GitHub.Unauthorized`, `GitHub.RateLimited`, `GitHub.NotFound`,
  `GitHub.InvalidResponse`, and `GitHub.Unavailable`. Descriptions never include the credential, the
  target URL, or a raw provider payload.
- The picker reads that code and says which thing is wrong: no working credential, rate limited, endpoint
  absent, or API down. Manual entry stays available in every case.

**Operator requirement.** Repository listing needs `GitHub:Token` in the API's user secrets or
environment. Without it GitHub answers 401 and the endpoint returns `GitHub.Unauthorized`, which is the
correct behaviour, not a bug. A fine-grained token with read-only Metadata, Contents, and Actions is
enough.

**Known limitation.** These endpoints are unauthenticated, like the rest of the API today. They expose
repository names the configured credential can see, so the API must not be exposed beyond localhost
until Console Ops has operator authentication.

## Principle

> Console Ops discovers whatever a provider already knows, and asks the operator only for what no
> provider can know.

Registration should feel like importing an application, not filling in a configuration form. The
operator should never retype a fact GitHub already holds.

What a provider knows: repository owner and name, default branch, workflow files and names, recent
workflow state, latest commit. What only the operator knows: the display name for the project, which
environment this instance represents, which workflow counts as a deployment, and the public base URL.

## Trust rules that constrain every phase

These are not negotiable and shape each phase's design:

- Every provider call belongs to the .NET API. Angular is the presentation layer: it calls
  `/api/...` and renders the result. No phase may put a GitHub call, a token, or an application probe in
  the browser, and no discovery heuristic belongs in TypeScript.
- Discovery may prefill; it may never silently decide. A suggested workflow is shown as `Suggested` and
  still requires confirmation.
- No screen displays a health, version, or sync result it has not actually observed. Until a probe has
  run, the state is `Not configured` or `Not checked`, never a plausible number.
- Probing an operator-supplied URL happens server side only, through the existing probe safeguards
  (explicit scheme, redirect limit, timeout, response-size cap, outbound-address checks). The browser
  never probes a target application directly: it would leak the operator's network position, break on
  CORS, and bypass every safeguard the API implements.
- Tokens and credentials are never returned by a discovery endpoint, never stored on a project, and
  never rendered.
- Every phase leaves the manual path working for projects with no connected repository.

## Phase 0 - Information architecture, no new endpoints

Implemented. Frontend only, against the frozen V1 `POST /api/projects`.

- Sections ordered as the operator thinks: Source, Project, Environment, Application.
- The environment name is derived from the chosen kind and hidden behind `Customize name`, so choosing
  `Production` no longer requires typing "Production".
- Removed `Save Draft`. Registration is short enough that a draft has no purpose, and the control had
  no behaviour behind it.
- Removed the `Connected Providers` panel. Provider roadmap state is product information, not project
  configuration, and `Azure - Later phase` did not belong inside a core workflow.
- Monitoring is stated as a consequence of configuration rather than as switches: an endpoint exists,
  so the observation runs. No boolean duplicates the endpoint's presence.
- The import path is visible as the intended primary route and explicitly unavailable, naming the phase
  that delivers it, so the screen tells the truth about what it can do today.
- The rail is an import preview with a real empty state instead of seven rows of `Not set`.

## Phase 1 - Repository discovery

Adds the repository picker. Removes typing `owner/name`.

Endpoint:

```text
GET /api/github/repositories?query=&page=
```

Returns, per repository: `owner`, `name`, `defaultBranch`, `private`, `pushedAt`, `htmlUrl`, and the
primary language when GitHub reports it. No tokens, no raw provider payloads.

Backend notes:

- Reuses the existing GitHub credential and adapter. The endpoint is a thin read behind the same port,
  with cancellation, timeout, and paging.
- Private repositories appear only because the configured credential can see them. The response says
  which are private so the UI can label them.
- The list is a provider read, not a stored entity. Do not persist a repository catalogue.

Frontend notes:

- A searchable picker replaces the repository text input; the manual field remains for the manual path.
- After selection, repository and default branch are displayed as discovered facts with a
  `Change repository` action, and the branch becomes a `Change source branch` action rather than a
  required input.
- Project name is prefilled from the repository name and stays editable, because presentation naming
  and repository naming legitimately differ.

## Phase 2 - Workflow discovery

Replaces the workflow filename textbox with a selection list.

Endpoint:

```text
GET /api/github/repositories/{owner}/{name}/workflows
```

Returns, per workflow: `name`, `path`, `state`, and the latest run's `conclusion` and `completedAt`
when GitHub provides them.

Rules:

- Console Ops must not select a workflow on its own. It may mark one `Suggested` from an explicit,
  documented heuristic on the workflow name, and the operator confirms.
- `No deployment workflow` is a first-class choice that leaves CI `notConfigured`.
- Each option shows its file path and last run state, so the operator recognises what they are picking
  instead of decoding filenames.

## Phase 3 - Pre-registration verification

Lets the screen prove the configuration before the project exists.

Endpoint:

```text
POST /api/projects/verification
```

Body: candidate `applicationUrl`, `healthUrl`, `versionUrl`. Response: per-endpoint reachability, HTTP
status class, duration, and the safe parsed version payload when the version endpoint returns one.

Rules:

- Same probe implementation and safeguards as the registered-project probes. This endpoint is a new
  caller of existing behaviour, not a second probe path.
- Rate limited per operator, since it accepts arbitrary targets. Failures are reported as observations,
  never as validation errors: an unreachable application must not block registration, because the
  application may simply not be deployed yet.
- No response body is stored or echoed. Only status, timing, and the recognised version fields.
- With this in place the screen can honestly show `Reachable - 103 ms`, `Health - Healthy`, and even
  `Source 8a17c2f / Deployed 8a17c2f - In Sync` before creation, because each line is a real
  observation with a timestamp. The source commit for that comparison comes from the already-imported
  repository, so version sync can be shown during setup rather than after the first refresh.
- When no endpoint path is known, the probe may try a small fixed list - `/health`, `/health/ready`,
  `/version` - and report which answered. The list stays short and hard-coded. Console Ops does not
  crawl an application's routes, and a path that answers is offered as a suggestion the operator
  confirms, never silently adopted.

## Phase 3a - Endpoint detection from source

Optional companion to Phase 3, and the reason endpoint fields can eventually disappear from the form.

```text
GET /api/github/repositories/{owner}/{repository}/endpoints?ref=
```

Reads a bounded set of source files through the GitHub Contents API and reports endpoint paths it
recognises, such as `MapHealthChecks("/health")` or a `MapGet("/version")` registration.

Rules that keep this honest:

- The result is `detected`, never `configured`. Detection is a heuristic over source text and can be
  wrong: a route may be composed from a `MapGroup` prefix, or read from configuration at run time, in
  which case Console Ops must report nothing rather than guess a path.
- The operator confirms every detected path before it is registered. A detected path that Phase 3 then
  fails to reach is shown as detected-but-unreachable, not as broken configuration.
- Bounded work: a capped number of files, a capped file size, and no repository cloning.
- Detection lives in .NET, in the same GitHub adapter as the rest of discovery. The browser never reads
  repository contents.

Only once detection and verification both exist does the form hide its endpoint inputs behind an
`Edit endpoints` action. Hiding them earlier would add a click to the common case instead of removing
one, because there would be nothing to prefill them with.

## Phase 4 - Confirmation and runtime import

- Registration returns to a short confirmation of what was actually created and verified - project,
  environment, source connected, workflow linked, endpoints verified - with `Open Project`. Every line
  reflects a completed step; nothing is listed optimistically.
- When Azure runtime awareness ships (V2), the same import pattern extends to runtime: list the
  operator's Container Apps, let them pick which resource belongs to this project, and never ask for a
  resource ID that the Azure API can enumerate.

## Sequencing and ownership

Phases are independent and shippable in order. Phase 1 and Phase 2 are read-only GitHub adapter work
and can land together if convenient. Phase 3 is the only phase that adds outbound requests to
operator-supplied targets and deserves its own review. Phase 4 depends on nothing but Phase 1 to 3
being present.

The frontend must not add a discovery UI before its endpoint exists. Until then the control stays
visible and disabled, naming the phase, exactly as Phase 0 does now.
