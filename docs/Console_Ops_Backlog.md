# Console Ops backlog

Status of 2026-08-16. This records what is left, what it depends on, and what must not be built yet.
Authority for behavior stays with `Console_Ops_Project_Context.md`, `Console_Ops_Architecture.md`, and
`Console_Ops_V1_API_Contract.md`; this file only tracks sequence.

## Shipped and real

| Capability | State |
|---|---|
| Register, edit, archive projects and environments | Real |
| GitHub source state and one configured workflow | Real |
| Health and version probes, deterministic version sync | Real |
| Overview, Projects, Environments, project detail | Real |
| Release history from GitHub Actions runs (Deployments) | Real |
| Scheduled collection (`Monitoring:Refresh`) | Real |
| Observed availability over 24h, with its sample count | Real |
| Browser re-reads without blanking screens | Real |
| Logs screen reading real Azure Container Apps console output | Real |
| Azure log-source discovery, so a scope is picked rather than typed | Real |
| Deployment and revision markers derived from recorded runs | Real |
| Cursor paging back through the provider's window, merged by id | Real |
| Framework-noise exclusion with a stated count and what wrote it | Real |
| Newest-first stream, per-line rendering, composed request lines | Real |
| Live tail following a scope from a time cursor | Real |
| Azure discovery across Container Apps and App Service, with per-resource status | Real |
| Configuration status by key name, with an opt-in credential probe | Real |
| Observation retention: a bounded batched sweep with a stated window | Real |
| Settings screen: integrations, collection, retention and build state | Real |
| Health screen: recorded checks, runs, availability and transitions | Real |
| Version state distinguishes not configured from configured but never read | Real |
| Application URL read from Azure, filling registration and editing | Real |
| Endpoint paths detected from repository source, offered in both project forms | Real |
| Workflows screen: provider-backed inventory with each workflow's latest run | Real |
| Workflow run jobs, read for the selected workflow | Real |
| Workflow run history with jobs per run, on its own screen | Real |
| Steps within a job, naming the step that failed | Real |
| Workflow screens follow a running run without being reloaded | Real |
| Operator-set workflow risk, gating whether Console Ops will run a workflow | Real |
| Starting a workflow, with declared inputs, an explicit ref and typed confirmation | Real |
| GitHub App sign-in: session, operator allow list, real identity in the top bar | Real |
| Steps within a job, naming the step that failed | Real |
| Workflow screens follow a running run without being reloaded | Real |
| Operator-set workflow risk, gating whether Console Ops will run a workflow | Real |
| Starting a workflow, with declared inputs, an explicit ref and typed confirmation | Real |
| GitHub App sign-in: session, operator allow list, real identity in the top bar | Real |
| Reading GitHub as the operator who asked, and as the service when nobody did | Real |
| Sessions that survive a restart and a second replica, and a sign-in that never answers JSON | Real |

## Next

1. **One Azure identity, and four stored credentials that stop being needed.** Console Ops currently proves who it
   is to Azure in three unrelated ways, and every one of them stores something. A managed identity on the container
   app, plus Entra authentication on Postgres, replaces all of it. The consumers, so this is not remembered as only
   a Logs concern:
   - **Logs and Azure discovery.** `DefaultAzureCredential` falls back to the ambient identity, which today means a
     developer's `az login`. Both features will fail once deployed. Needs Reader on the subscription and Log
     Analytics Reader on the workspace.
   - **`DataProtection:BlobUri`.** A SAS on the key blob is only necessary because the container has no identity of
     its own. With one, the plain blob URI is read directly - the configuration shape already allows either. Needs
     Storage Blob Data Contributor on that one container.
   - **The deploy workflow's `PROD_DB_CONNECTION`.** It exists only because the runner cannot see the container
     app's connection string, and reading it at deploy time would be worse: GitHub masks secrets it was told about,
     not values fetched at run time, so one careless `echo` would put a production password in a workflow log for
     good. `activeDirectoryAuth` is `Disabled` on the server today; enabling it lets the runner authenticate as the
     deployment identity with a short-lived token and store no password at all.
   - **The container app's own `db-connection` secret**, for the same reason and at the same time.

   It is not one change: the server needs Entra auth enabled and an Entra administrator, the identity needs rights
   inside the database, and three role assignments have to be made. But it is one identity, and each consumer stops
   holding a credential rather than holding a better-managed one.
2. **One GitHub credential instead of two.** An App installation token could replace the worker's configured
   `GitHub:Token`, which exists only because scheduled collection runs with no operator. Reads made for a person
   already use that person's token.
3. **More than one operator on one Console Ops.** Sign-in admits several logins, but nothing yet says whose project
   a project is: the catalogue is global, so a second operator would see and could dispatch the first one's
   workflows. Two pieces, in order - **an owner and invitations** so adding somebody is not an environment variable
   edit (the first sign-in claims an unclaimed instance, and the allow list stays as an override), then
   **per-operator ownership of projects**, which is a domain change and the honest prerequisite for sharing. Until
   both exist, one instance means one operator.
4. **Console Ops cannot start a workflow with the current token.** Dispatch is built and verified end to end, and
   GitHub refused it: the configured token has read access to Actions but not write, which the API reports as
   `Workflows.Unauthorized` with a 403. Granting write on Actions is an operator action; nothing in the code
   changes when it is. Re-run and cancel are the natural follow-ups once a run can be started.
   Worth knowing: `Azure.Unauthorized` still maps to 500 while `Workflows.Unauthorized` maps to 403. The Workflows
   answer is the correct one - a missing token scope is not a server fault - and Azure should follow, which is a
   change to an existing contract rather than part of this slice.
5. **Workflow execution logs.** The last of the read side: GitHub serves them as a zip archive per run, which is
   why `Run logs` is still named as planned rather than half-built.
6. **Cross-cutting Workflows follow-ups.** **Cross-link Deployments to Workflows** so a release reaches the run
   that built it; **converge the two workflow-listing ports** (`IGitHubRepositoryCatalog.ListWorkflowsAsync` and
   `IGitHubWorkflowInventory.ListWorkflowsAsync` list workflows for different callers) now that the richer one is
   proven; and **a deployment workflow is recorded against the project, not an environment**, so no environment
   is named on the screen - the feature context wants zero or one per environment, which is a domain change.
7. **An App Service log reader — blocked on collection, not on code.** StallTrack runs on App Service, and
   discovery now lists both of its sites (`stalltrack-api-cly-2026`, `stalltrack-web-cly-2026`) with
   `platformNotSupported`, so the screen no longer stays silent about them. The reader itself is deliberately
   not written yet: **neither site has a diagnostic setting**, there is no Application Insights resource in
   the subscription, and a union over `AppServiceConsoleLogs`, `AppServiceHTTPLogs`, `AppServiceAppLogs`
   returned **zero rows over 30 days** in both workspaces. Writing a KQL reader against documented column
   names with nothing to verify it against is exactly how the `ContainerAppConsoleLogs_CL` mistake happened.
   Unblocked by one operator action in Azure: create a workspace in `stalltrack-prod-rg` (or reuse one) and
   add a diagnostic setting on the site sending `AppServiceConsoleLogs`. Console Ops has read-only Azure
   access by rule and cannot do it. Once rows exist, the reader is a platform discriminator on the log source,
   a reader chosen by that platform, and a KQL query verified against those rows.
8. **Logs Phase 5 - richer telemetry.** Structured JSON console logging in the monitored applications is the
   cheapest option that keeps the pull model: trace ids and properties travel through the same Azure table.
   OpenTelemetry next. A Console Ops collector last, because only it needs inbound exposure.
9. **Version endpoints on the monitored applications.** Not Console Ops work, but it blocks the most
   valuable correlation on the Deployments screen: while no environment reports a commit, every release
   reads `Unverified` and no release can be marked current. Expected payload:
   `{ "application": string, "version": string, "commit": "<40-hex>", "environment": string, "builtAt": ISO }`.
   The field is `commit`, and a short SHA is rejected rather than guessed at. Measured on the registered
   projects: StallTrack's `/version` answers `200` with the Angular application, Spinner's answers `401`, and
   EEMO's `301`s - all three are configured and unreadable, which the Overview now reports as `Not reported`
   rather than as unconfigured.
10. **EEMO and StallTrack monitor one API.** The operator confirmed a single backend serves both, so two
   projects hold health checks against the same host and their verdicts can never disagree. They deploy from
   different repositories, which argues for keeping both projects - but only one should own the health check,
   or EEMO should become an environment of StallTrack. Undecided, and recorded so it is not rediscovered.
11. **Spinner's health and version URLs point at the Vercel frontend**, not the Container Apps API, so
   `/version` 404s and every release reads `Unverified`. Operator-side and in the Spinner repository, which
   Console Ops work does not touch: it is recorded here so the cause of `Unverified` is not investigated
   twice.

## Blocked or deferred



- **The deploy workflow applies migrations and needs one secret that only an operator can add.**
  `PROD_DB_CONNECTION` is required: without it the deploy job stops at a preflight step that names it, which leaves
  the previous revision serving - the intended failure, but the deployment does not move. `AZURE_POSTGRES_SERVER` is
  optional and only opens a firewall rule for the runner; without it the migration is attempted directly, since a
  GitHub-hosted runner may already be reachable through the server's allow-Azure-services rule. Both stop being
  needed once the database accepts Entra authentication, which is item 1 in Next.
- **`DataProtection:BlobUri` is required in production and not yet set.** A storage account, a private container,
  and either a SAS on the blob or managed identity with Storage Blob Data Contributor. Until it is set, sessions do
  not survive a restart and a second replica cannot decrypt what the first one wrote. The SAS is only necessary
  while the container app has no identity of its own - see item 1 in Next.
- **Azure runtime awareness (V2).** Unlocks the runtime revision, the runtime target, and a `Current`
  release derived from the runtime itself rather than only from `/version`. Also unlocks the platform
  source on the Logs screen (revision activated, image pull failure, replica problems).
- **Docker awareness.** Local container lifecycle events for the Logs screen's runtime source.
- **Deployment controls.** Trigger, redeploy, rollback. Product phase decision, not a UI gap: the buttons
  exist as disabled controls that name the phase they belong to.
- **Trace correlation across services.** The Logs contract carries `traceId` and `requestId` so the
  detail rail can offer "view related logs" once ingestion exists. OpenTelemetry alignment comes with it.
- **Error grouping** on the Logs screen (repeated exceptions collapsed with first and latest seen).
  Deliberately later; V1 stays chronological.
- **Real authentication.** The shared key is an exposure guard, not an identity system. Needed only if
  Console Ops ever serves more than one operator.

## Rules that constrain this backlog

- Never fabricate operational state. A mock is acceptable only while it is labelled as sample data, and
  it must be deleted rather than kept as a fallback when the real source lands.
- No provider calls from the browser. Collection is server side and scheduled.
- CI/CD execution logs belong to the workflow run on the Deployments screen, not to the Logs stream. The
  Logs stream is application, runtime, and platform events.
