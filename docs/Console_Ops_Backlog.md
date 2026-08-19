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
| Workflows screen | Labelled sample data; the provider read is the next slice |

## Next

1. **Workflows read from the provider.** The screen exists with sample data labelled in its own payload
   (`isSampleData`), and `docs/Console_Ops_Workflows_Context.md` is the product authority for it. What the
   real slice needs: list a repository's workflows and each one's latest run through the GitHub port, a
   `WorkflowsDataSource` HTTP adapter replacing `MockWorkflowsDataSource`, and the sample adapter **deleted**
   rather than kept as a fallback. Run history, run detail, jobs and steps follow as their own screens - the
   inventory row deliberately links to them as planned rather than pretending they exist.
   Two deviations from the reference mockup are deliberate and must not be "fixed" back:
   **classification filters offer only `Deployment` and `Unclassified`**, because a provider reports no
   business category and chips for CI, Maintenance and Release would imply Console Ops knows one; and
   **workflow icons are one generic glyph plus a deployment glyph**, because a database icon for "Database
   backup" is inferred from its name. Manual running, workflow logs and artifacts stay later phases.
2. **An App Service log reader — blocked on collection, not on code.** StallTrack runs on App Service, and
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
3. **Logs Phase 5 - richer telemetry.** Structured JSON console logging in the monitored applications is the
   cheapest option that keeps the pull model: trace ids and properties travel through the same Azure table.
   OpenTelemetry next. A Console Ops collector last, because only it needs inbound exposure.
4. **Version endpoints on the monitored applications.** Not Console Ops work, but it blocks the most
   valuable correlation on the Deployments screen: while no environment reports a commit, every release
   reads `Unverified` and no release can be marked current. Expected payload:
   `{ "application": string, "version": string, "commit": "<40-hex>", "environment": string, "builtAt": ISO }`.
   The field is `commit`, and a short SHA is rejected rather than guessed at. Measured on the registered
   projects: StallTrack's `/version` answers `200` with the Angular application, Spinner's answers `401`, and
   EEMO's `301`s - all three are configured and unreadable, which the Overview now reports as `Not reported`
   rather than as unconfigured.
5. **EEMO and StallTrack monitor one API.** The operator confirmed a single backend serves both, so two
   projects hold health checks against the same host and their verdicts can never disagree. They deploy from
   different repositories, which argues for keeping both projects - but only one should own the health check,
   or EEMO should become an environment of StallTrack. Undecided, and recorded so it is not rediscovered.
6. **Spinner's health and version URLs point at the Vercel frontend**, not the Container Apps API, so
   `/version` 404s and every release reads `Unverified`. Operator-side and in the Spinner repository, which
   Console Ops work does not touch: it is recorded here so the cause of `Unverified` is not investigated
   twice.

## Blocked or deferred



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
