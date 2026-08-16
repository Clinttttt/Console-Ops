# Console Ops - Logs plan (provider-backed)

## Status

Planning, 2026-08-16. **Phase 1 is implemented**; Phases 2 to 5 remain planned. Supersedes the
ingestion-first draft, which is withdrawn.

Phase 1 as built:

- `ProjectEnvironment` carries an optional `AzureLogSource` (workspace id + container app name) as an
  owned value object, with migration `AddEnvironmentAzureLogSource` adding two nullable columns.
- The domain refuses half a source; the API refuses it as a validation problem rather than a fault; both
  Add Project and Edit Project expose the two optional fields with the same rules.
- `IApplicationLogReader` is the port. `AzureMonitorLogReader` implements it with `LogsQueryClient`,
  bounded by `Azure:Monitor` options (rows, window, timeout) and mapping provider errors to the same
  failure vocabulary the GitHub reader uses.
- `AzureConsoleLogQuery` builds a fixed-shape KQL query over `ContainerAppConsoleLogs` and escapes
  operator text as a literal. `AzureConsoleLogNormalizer` recovers level and category from the .NET
  console convention, folds continuation lines into a stack trace, derives a level from `stderr` only when
  no prefix exists, and synthesizes a deterministic id per row.
- Credentials come from Console Ops' own configuration - a service principal when `Azure:TenantId`,
  `Azure:ClientId`, and `Azure:ClientSecret` are all present, otherwise the ambient Azure identity. Never
  per project.

Not yet verified against a real workspace: that needs the open decisions below answered.

**Decision: Console Ops keeps pulling.** The first real log source is Azure Container Apps through Azure
Monitor Log Analytics. Console Ops does not accept inbound logs, does not ship an `ILoggerProvider`, does
not issue ingestion keys, and gains no internet-facing collector. Application-pushed structured telemetry
(OpenTelemetry or a Console Ops collector) is revisited only once the provider-backed slice is stable.

This keeps the model every other feature already follows:

```text
GitHub    -> Console Ops reads
/health   -> Console Ops reads
/version  -> Console Ops reads
Azure     -> Console Ops reads      <- this plan
```

Authority: `Console_Ops_Project_Context.md` for scope, `Console_Ops_Architecture.md` for boundaries,
`Console_Ops_V1_API_Contract.md` for transport. The frontend contract in
`src/app/core/contracts/log-stream.ts` is the target shape; this plan lists the changes it needs to stay
honest about what Azure can actually provide.

## What Azure actually gives us

Verified against the `ContainerAppConsoleLogs` table reference (Azure Monitor, retrieved 2026-08-16).
Two corrections to assumptions worth recording, because they change the design:

- The documented table is **`ContainerAppConsoleLogs`** with plain column names. `_CL` / `_s` suffixes
  belong to the legacy custom-table form and must not be hardcoded from memory.
- Columns available: `TimeGenerated`, `Log`, `Stream` (stdout/stderr), `ContainerAppName`,
  `RevisionName`, `ContainerName`, `ContainerImage`, `ContainerGroupName` (the replica),
  `ContainerGroupId`, `EnvironmentName`, `Location`, `_ResourceId`.

What is **not** there: no level, no category, no message template, no trace id, no properties, no
exception object. The row is a line of stdout or stderr plus runtime identity.

Consequences that shape everything below:

1. **Level and category are parsed, not received.** ASP.NET's console writer emits
   `warn: Spinner.Payments[0]` followed by an indented message line. That prefix is a convention, so
   parsing it is a best effort - and when it does not match, the level is `unknown`. It must never be
   guessed as `info`.
2. **TraceId and properties stay empty for now.** The contract keeps them because they are the reason the
   detail rail exists; the screen must render their absence as "Not provided", which it already does.
3. **An exception spans several rows.** Azure stores one row per line, so a stack trace arrives as many
   rows. Joining them is a rule, not a guess: continuation lines are the ones that do not begin a new
   `level: category[id]` prefix.
4. **Retention is Azure's, not ours.** The window comes from the workspace, so the screen states the
   range it queried rather than implying complete history.
5. **`RevisionName` on a log row is the revision that emitted that line.** It is evidence about a log
   event, not a claim about which revision is currently serving. V1 still must not report a current
   runtime revision on Environments or Deployments; that needs the Container Apps control-plane API and
   remains a V2 item.

System and platform events live in a separate table (`ContainerAppSystemLogs`). They are the `runtime` and
`platform` sources in the contract and are deliberately out of this plan's V1.

## The one pattern deviation, stated plainly

Every existing screen reads **stored observations**: a refresh collects, the browser re-reads our
database. Logs cannot follow that shape without copying Azure's log store into Postgres, paying for the
duplication, and inventing our own retention on top of Azure's.

So the Logs query is Console Ops' first **pass-through provider read**: the server queries Azure during
the request and normalizes the result. The steering rule that matters is unchanged - the browser still
never talks to a provider - but the trade-off is real and must be accepted deliberately:

| | Stored observations | Pass-through (Logs) |
|---|---|---|
| Freshness | as of last sweep | as of the request |
| Provider outage | screen goes stale | screen reads `unavailable` |
| Cost | one sweep per interval | one query per read |
| Storage | ours to prune | Azure's |

Guardrails that make it acceptable, all required in Phase 2:

- a hard maximum time range and row cap per query;
- a short-lived in-memory response cache (about 10-15 seconds) so a polling tail does not bill Azure per
  keystroke;
- a per-project query rate limit, reusing the existing fixed-window limiter pattern;
- an explicit timeout, with the failure surfaced as the screen's existing `unavailable` state and never
  as an empty stream, because "no logs" and "we could not ask" are different facts.

`Console_Ops_Architecture.md` gains this as a decision record when Phase 2 lands.

## Phase 1 - Azure log source: configuration and adapter

No UI change. Two halves.

### Configuration: where a scope's logs live

An environment gains an optional Azure log source. Without it, that environment simply reports
`Not configured`, exactly as a missing health endpoint does today.

- `ProjectEnvironment` gains `AzureLogWorkspaceId` and `AzureContainerAppName`, both optional, validated
  in the domain (workspace id is a GUID; the app name matches Azure's naming rules). Kept as a small
  owned value object rather than two loose strings, so "half-configured" cannot exist.
- One migration, additive. Register/Update project commands, validators, and the Add/Edit Project screens
  carry the two fields as an optional Azure section.
- Credentials are **not** per project and never in the database. `DefaultAzureCredential` in
  Infrastructure, configured by environment variables or user-secrets
  (`Azure:TenantId`, `Azure:ClientId`, `Azure:ClientSecret`) or a managed identity when hosted. Console
  Ops needs only the `Log Analytics Reader` role on the workspace - read-only, least privilege.
- Configuration status is reported by key name only, as the security rule requires.

### Adapter behind a port

```text
Application/Integrations/AzureMonitor/IApplicationLogReader.cs   port + app-owned models
Infrastructure/Integrations/AzureMonitor/AzureMonitorLogReader.cs  KQL + SDK, DTOs stay internal
```

- `Azure.Monitor.Query`'s `LogsQueryClient`. SDK types never leave the adapter, matching the GitHub and
  probe adapters.
- The port takes a normalized request (workspace, container app, window, level, search, limit, cursor)
  and returns application-owned `ApplicationLogEntry` records plus a failure enum in the same shape as
  `GitHubReadFailure`: `Unauthorized`, `NotFound`, `RateLimited`, `Unavailable`, `InvalidResponse`.
- **KQL is built from a fixed template.** Identifiers come from validated configuration and are never
  interpolated raw; operator-supplied search text is escaped as a KQL string literal, with tests for
  quotes, backslashes, and newlines. A log screen that accepts free text is a query-injection surface,
  and the adapter is where that is closed.
- Ordering is `TimeGenerated desc` with a row cap. Basic-tier tables restrict the KQL subset available,
  so the query stays to `where`, `project`, `parse`, `order`, and `take` - no joins, no aggregation.
- Normalization lives in the adapter and is tested against captured sample rows:
  - level and category parsed from the ASP.NET console prefix, `unknown` when absent;
  - `Stream = stderr` raises an unparsed line to `error` only when the prefix gave nothing, and that
    inference is recorded so the UI can say the level was derived;
  - continuation lines fold into the preceding entry's message or stack trace by the rule above;
  - a **deterministic synthetic id** per entry - a hash over timestamp, replica, stream, ordinal within
    that timestamp, and message - because the provider exposes no stable row id and the UI needs a
    stable `id` for selection across pages. Documented as ours, not Azure's.

Tests: unit tests for prefix parsing, continuation folding, level derivation, id stability, and KQL
escaping. Integration tests for the adapter against a stubbed transport in the style of
`GitHubProjectReaderTests`, including each failure mapping.

## Phase 2 - Logs query slice, and the mock goes away

`GET /api/logs` producing the frontend's `LogStream`:

- required `projectId` and `environmentId` - a stream always belongs to one scope;
- optional `level`, `sourceKind`, `search`, `before`, `after`, `limit` (clamped, default 200);
- time/cursor paging on `(occurredAt, id)`, never offset, since the window moves while it is read;
- the response states the queried window, the provider, and whether the row cap truncated the result;
- scopes come from the operator's own configured projects and environments, so the toolbar keeps working
  exactly as it does against the fixture.

Then: register `HttpLogStreamDataSource`, delete `log-stream.fixture.ts`,
`mock-log-stream.data-source.ts`, and the SAMPLE DATA banner. The mock is deleted, not kept as a
fallback - same discipline as Deployments.

### Contract changes needed to stay honest

| Change | Why |
|---|---|
| `LogLevel` gains `'trace' \| 'debug' \| 'critical' \| 'unknown'` | ASP.NET writes `trce/dbug/warn/fail/crit`, and a plain line has no level at all |
| `source` becomes nullable | a line without a category prefix has no category |
| `LogEvent` gains optional `revision` | Azure reports the emitting revision per row |
| `LogEvent` gains optional `stream` (`stdout`/`stderr`) | it is real, cheap, and explains a derived level |
| `LogEvent` gains optional `levelIsDerived` | so a parsed level is never presented as one the app declared |

The toolbar keeps four filters. `INF` covers informational levels including debug and trace, `ERR`
covers error and critical, and `unknown` appears only under `All` - a mapping the UI owns and documents,
so the contract stays machine-readable and the screen stays readable.

## Phase 3 - Deployment correlation

Markers stay **derived from what Console Ops already recorded**: the `deployments` table and version
observations, joined in our own database, never written into a log store and never read from Azure. A
marker appears because a run of that project completed inside the window being read.

`RevisionName` from the log rows enriches a revision marker where it agrees with what we recorded. Where
it does not agree, we show what we observed and say nothing about the difference.

## Phase 4 - Live tail

Polling first, as decided: the same query with `after=<last id>` on a short interval while `Live` is on,
appending rather than replacing. Pause stops asking. The response cache and rate limit from Phase 2 are
what make this affordable.

Azure's own log streaming or server-sent events are considered only if polling proves too slow in
practice. No SignalR without a requirement.

## Phase 5 - Revisit richer telemetry

Only after the provider-backed slice is stable, and only if the parsed console stream proves too thin.
Then choose deliberately between:

- **structured JSON console logging** in the applications, which keeps the pull model and immediately
  gives properties and trace ids through the same Azure table;
- **OpenTelemetry** via the Container Apps managed agent to a collector;
- **a Console Ops collector endpoint**, which is the only option that requires making Console Ops
  externally reachable and is therefore the last resort.

Deliberately not decided now: whether a sink is a pasted file or a small package. That question only
exists if a collector is ever built.

## Rules that constrain every phase

1. Never present a queried window as complete history. State the range.
2. Level and category come from the provider's text or are marked as derived. Nothing is invented.
3. Provider text is untrusted: rendered as text, capped, never as HTML.
4. Console Ops asks Azure for read-only data with least privilege, and no credential is stored per
   project or displayed anywhere.
5. Bounded everywhere: time range, row cap, cache TTL, rate limit, page size, timeout.
6. CI/CD execution logs stay on the Deployments screen. This stream is application, and later runtime and
   platform, events.
7. V1 is `application` (console) only. `runtime` and `platform` follow from `ContainerAppSystemLogs` once
   the application stream is working.

## Sequencing and ownership

| Phase | Backend | Frontend | Blocked by |
|---|---|---|---|
| 1 | Azure log source config + migration, port, adapter, normalization | Add/Edit Project: optional Azure section | credentials and a workspace to read |
| 2 | query slice, caps, cache, rate limit | contract updates, HTTP adapter, delete mock and banner | Phase 1 |
| 3 | marker join from our own tables | none | Phase 2 |
| 4 | `after` cursor | tail interval while Live | Phase 2 |
| 5 | decision, then a slice | depends on the decision | Phase 2 in use |

One agent owns the migration in this batch. Each phase builds and tests green before the next begins.

## Open decisions

1. **Which workspace and container app back each environment?** Needed before Phase 1 can be verified
   against anything real: the Log Analytics workspace id and the Container App name for
   Spinner / StallTrack / EEMO. Also confirm the apps are on Container Apps rather than App Service, since
   App Service logs live in different tables and would need a second adapter.
2. **Credential shape.** Recommendation: a service principal with `Log Analytics Reader` on the workspace,
   in user-secrets for local use; managed identity if Console Ops is ever hosted.
3. **Maximum window and row cap.** Recommendation: 24 hours and 1000 rows per query, both configurable,
   with the screen stating both.
4. **Structured JSON console logging.** Not required by this plan, but it is the cheapest path to real
   properties and trace ids while keeping the pull model. Worth considering during Phase 5 rather than
   changing course now.
