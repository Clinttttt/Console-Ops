# Console Ops - Logs plan (provider-backed)

## Status

Planning, 2026-08-16. **Phases 1, 1b (discovery), 2, and 3 are implemented**; Phases 4 and 5 remain
planned. Supersedes the ingestion-first draft, which is withdrawn.

Phase 3 as built, and corrected by real data:

- Markers are composed at query time from the `deployments` rows a refresh already wrote. There is no
  marker table and no second collection path, so Deployments and Logs cannot tell different stories.
- A **deployment marker** carries the short commit, the recorded release id, and no revision: a run proves
  CI built a commit, not that a particular revision started serving it.
- Markers are bounded by the **events on screen**, not by the requested window. A marker below the oldest
  line the provider returned would sit where nothing it explains can be seen.
- A **revision marker** is emitted once per revision, at the earliest line Console Ops has from it, and
  never for the revision that was already serving when the window opened.
- The UI says **"Revision first seen"**, not "Revision started". Console output shows which revision emitted
  a line, not when that revision began serving.
- Measured live against run #35 of Spinner: `Deployment 280dd86` at 04:56:14, then
  `Revision first seen spinner-api-stg--0000043` at 04:56:18 - two markers over 106 real events.

### Correction: a change-detecting revision rule flaps during a rollout

The first implementation marked a revision wherever two neighbouring lines reported different revisions.
Against a real deployment that produced **three markers for two revisions** (43 -> 42 -> 43), because during
a rollout the outgoing revision keeps logging while the incoming one starts and their lines interleave. Two
of the three also claimed a revision had started when it had been serving all along. One marker per
revision, at its earliest line, is what the rows actually support. Pinned by
`Stream_WhenTwoRevisionsOverlapped_MarksEachOnceInsteadOfFlapping`.

### Correction: the tagged union needs its tag on the wire

`items` is a tagged union and the screen selects on `kind`. The API did not send it, so every real event was
unrecognizable to the client and a correct 200 with seventeen events rendered as an empty stream. The
frontend specs passed throughout because their fixtures were hand-written to the contract, with `kind`
typed in by hand.

The tag is now emitted by the serializer (`JsonPolymorphic` over `LogStreamItemResponse`), so a new item
type cannot forget to carry one. Two tests guard it: one asserts the raw JSON contains `"kind":"event"`, and
a frontend spec renders the page from a payload captured verbatim from a live response, parsed at runtime so
TypeScript cannot supply a field the server omits.

Phase 2 as built, and verified against the operator's own workspace:

- `GET /api/logs` reads one project environment's console output through Azure Monitor and returns the
  screen's contract. The fixture, its mock adapter, and the SAMPLE DATA banner are deleted.
- The adapter reads **both table shapes** with `union isfuzzy=true`, and orders by `time_t`, after the two
  corrections recorded below.
- Scope and free-text search are pushed down to the provider, because a 24-hour window holds far more lines
  than a page; severity and source-kind narrow what is already on screen.
- The response states the window and whether the row cap truncated it. Failure is never emptiness: no
  configured source, a rejected identity, and a provider that could not be asked each get a distinct code
  and message.
- Measured live: 200 in about 7s on a cold credential, real revision and replica identity, ingestion delay
  of roughly half a second visible between the two clocks, and prefix parsing recovering
  `Microsoft.EntityFrameworkCore.Database.Command` as the category from real lines.

Phase 1b as built - the operator picks instead of typing:

- `GET /api/azure/log-sources` lists container apps the configured Azure identity can see, each carrying
  the Log Analytics workspace its Container Apps environment logs to, so one pick fills both fields.
- `IAzureLogSourceCatalog` is the port; `AzureResourceGraphCatalog` implements it with one read-only
  Resource Graph query, bounded to 200 rows, with filter text escaped as a literal.
- An app whose environment has no workspace is shown as unavailable rather than offered, and a rejected
  credential is reported as `Azure.Unauthorized` rather than as an empty tenant.
- The picker owns its own trigger and only calls Azure when opened. The two fields stay editable
  afterwards: discovery may prefill but never decide.

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

Verified against the `ContainerAppConsoleLogs` table reference (Azure Monitor, retrieved 2026-08-16) and
then **against the operator's own workspace on 2026-08-16**, which corrected two assumptions.

### Correction: this tenant's logs are in the legacy table

The documented reference table is `ContainerAppConsoleLogs` with plain column names. The operator's
Container Apps environments write to **`ContainerAppConsoleLogs_CL`** with `_s`-suffixed columns instead -
the legacy custom-table form. Both exist in the wild, and querying only the documented one returns nothing
here even though the environment is configured correctly (`appLogsConfiguration.destination` is
`log-analytics` and the workspace id matches).

Measured in the Spinner staging workspace: **702,893 rows in 30 days**, latest within the hour. The
adapter must therefore read both shapes - `union isfuzzy=true` over the two tables, projecting them to one
column set - rather than assume either.

| Legacy (`_CL`) | Modern |
|---|---|
| `Log_s` | `Log` |
| `Stream_s` | `Stream` |
| `RevisionName_s` | `RevisionName` |
| `ContainerAppName_s` | `ContainerAppName` |
| `ContainerGroupName_s` | `ContainerGroupName` |
| `time_t` | *(no equivalent; use `TimeGenerated`)* |

### Correction: `TimeGenerated` is the received time, not the emit time

In the legacy table every row of one ingestion batch shares a single `TimeGenerated`, so ordering by it
**scrambles the lines within a batch** - an observed `info:` prefix line appeared in the middle of its own
SQL block. The container's own emit timestamp is `time_t`, with sub-microsecond precision, and ordering by
it restores exact line order.

So the plan's two-clock rule maps onto real columns: `time_t` is the emitter's clock and what the stream
orders and displays by; `TimeGenerated` is when Azure received it. Continuation folding only works when
ordered by `time_t`.

### What the rows contain

Confirmed from real output: the .NET console convention is used
(`info: Microsoft.EntityFrameworkCore.Database.Command[20101]` followed by indented lines), so prefix
parsing and the category anchor hold. The volume is dominated by EF Core SQL, which is exactly the noise
the severity filter and continuation folding exist to tame.

What is still **not** there: no level column, no category column, no message template, no trace id, no
properties, no exception object.

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

**Built.** Two departures from the plan above, both forced by real data and recorded in Status: the window
that bounds markers is the visible events rather than the requested window, and a revision marker is one
per revision rather than one per observed change. Version observations were not needed - the recorded run
plus the revision the rows report is the whole story - so nothing was joined that a marker did not use.

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
