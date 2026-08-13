# Console Ops agent guidance

Before architecture or feature work, read:

- `docs/Console_Ops_Project_Context.md` for product behavior and scope.
- `docs/Console_Ops_Architecture.md` for implementation rules and dependency boundaries.

Treat Console Ops as a pragmatic modular monolith using Vertical Slice Architecture, CQRS with
MediatR, and focused DDD. Implement one end-to-end use case at a time. Do not introduce a generic
repository, event sourcing, microservices, provider SDK types outside Infrastructure, or features
beyond the current product phase without a concrete requirement.

Never reveal or copy configuration secrets. In particular, inspect configuration by key name when
possible and keep connection strings, tokens, and credentials out of source, logs, documentation,
tests, and responses.

The AMYL.Api project may be consulted for ideas, but `docs/Console_Ops_Architecture.md` is the
authority for Console Ops and lists the reference patterns that must be corrected rather than copied.

When multiple coding agents edit concurrently, give each agent/task its own Git worktree and branch
outside the primary checkout. Keep the primary worktree for integration, avoid overlapping slice or
migration ownership, and integrate focused commits one task at a time. Read the full multi-agent Git
workflow in `docs/Console_Ops_Architecture.md`. Read-only or sequential work does not require another
worktree.
