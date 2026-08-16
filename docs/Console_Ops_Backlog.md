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

## Next

1. **Logs screen, design mock first.** Requested 2026-08-16. A labelled sample-data mock of the forensic
   stream: chronological application, runtime, and platform events, deployment and revision markers for
   context, and a detail rail with correlation ids, structured properties, and a collapsed stack trace.
   Data comes later; nothing on it may be presented as observed until it is.
2. **Provider-backed logs, Azure first.** Planned in `Console_Ops_Logs_Plan.md`. Console Ops keeps
   pulling: the first log source is Azure Container Apps console logs through Log Analytics, normalized
   behind a port like every other provider. No inbound ingestion, no `ILoggerProvider`, no ingestion
   keys, no internet-facing collector. V1 is application console logs only; system and platform events
   follow from `ContainerAppSystemLogs`. Application-pushed structured telemetry is revisited only once
   this slice is stable.
3. **The first Azure integration.** Phase 1 of that plan establishes the Azure adapter seam and an
   optional per-environment Azure log source in project configuration. It deliberately does **not** claim
   a current runtime revision: a revision name on a log row says which revision emitted that line, not
   which one is serving now. That still needs the Container Apps control-plane API (V2).
4. **Settings: configuration status.** A small endpoint reporting `Configured` / `Missing` **by key name
   only** for the keys Console Ops needs (`GitHub:Token`, `ConnectionStrings:DefaultConnection`,
   `Api:Key`, `Monitoring:Refresh`, and the Azure credential keys once Logs Phase 1 lands). Never a value.
   Would have diagnosed the missing GitHub token instantly.
5. **Version endpoints on the monitored applications.** Not Console Ops work, but it blocks the most
   valuable correlation on the Deployments screen: while no environment reports a commit, every release
   reads `Unverified` and no release can be marked current. Expected payload:
   `{ "application": string, "version": string, "commit": "<40-hex>", "environment": string, "builtAt": ISO }`.
   The field is `commit`, and a short SHA is rejected rather than guessed at.

## Blocked or deferred

- **Health screen.** Parked at the operator's request while its content is decided. Mostly feasible from
  data already collected: per-environment health, dependencies from the health payload, health
  transitions, and the availability window. Do not build it before that decision.
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
