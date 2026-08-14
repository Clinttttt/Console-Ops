---
name: console-ops-frontend
description: Recipe for the Console Ops Angular app - workspace layout, typed contracts, data-source ports, signal stores, standalone component conventions, the restrained design system, and honest unknown-state rendering. Use when writing or reviewing anything under repos/frontend/console-ops-web.
---

# Console Ops frontend recipe

Authority: `docs/Console_Ops_Project_Context.md` for behavior, `docs/Console_Ops_Architecture.md` for
boundaries, and `docs/Console_Ops_V1_API_Contract.md` for V1 transport semantics. This skill records
how the existing app is built so new work matches it.

Workspace: `repos/frontend/console-ops-web` (Angular 22, TypeScript 6, standalone components, SCSS,
zoneless, vitest). Scripts: `npm start`, `npm run build`, `npm test -- --no-watch`, `npm run lint`,
`npm run format`, `npm run verify` (lint + format check + tests + production build).

## Layout

```text
src/
|-- styles.scss                             design tokens + shared primitives
`-- app/
    |-- app.ts|html|scss                    shell: sidebar + top bar + router outlet
    |-- app.config.ts                       providers, including the data-source swap point
    |-- app.routes.ts                       routes carry `data.title` / `data.subtitle`
    |-- core/
    |   |-- contracts/                      typed API contracts (transport shaped)
    |   |-- data/                           ports + adapters (`mock/` holds the fixture adapter)
    |   |-- state/                          signal stores
    |   |-- layout/                         sidebar/, top-bar/
    |   `-- ui/                             icon, status, sparkline, environment-tag, project-mark
    `-- features/
        `-- overview/                       page + `components/` sections
```

Feature-first: a screen owns its page component, template, styles, sections, and spec. Something moves
into `core/` only when a second feature needs it.

## Conventions

- Standalone components with `changeDetection: ChangeDetectionStrategy.OnPush`, `input()` /
  `input.required()` / `output()`, and `computed()` for derived values. No `NgModule`, no decorator
  `@Input`/`@Output`.
- Element prefix `co-` (`app-root` stays for the bootstrapped root). Class names are plain nouns:
  `Sidebar`, `TopBar`, `OverviewPage`, `ProjectSurfacesSection`.
- Files: `kebab-case.ts` with sibling `.html` / `.scss` for anything table-heavy; short presentational
  primitives may keep an inline template and `styles`.
- Built-in control flow only: `@if`, `@for` (always with `track`), `@switch`, `@let`.
- State lives in signals. NgRx is not permitted until state complexity proves it necessary.
- Keep per-component styles under the 4 kB `anyComponentStyle` budget; shared visual language belongs
  in `styles.scss`.
- Templates must stay accessible: real `<table>` semantics with `scope`, labelled controls,
  `aria-label` on icon-only buttons, `aria-disabled` on planned destinations. The `angular-eslint`
  template accessibility rules run in `npm run lint`.

## Data flow

```text
contract (core/contracts) -> port (core/data) -> adapter (mock or http) -> store (core/state) -> page
```

- A contract file mirrors one API response, uses `readonly` members, ISO-8601 UTC strings, and `null`
  for anything the platform could not establish. It is the shared reference for the backend slice.
- A port is an `abstract class` used directly as the DI token. Components never inject an adapter.
- V1 registers `MockDashboardOverviewDataSource` in `app.config.ts`. Replacing that one provider with
  the HTTP adapter is the entire real-data migration; the mock is then deleted, never kept as a
  fallback.
- Stores are `providedIn: 'root'`, expose readonly signals, and track load outcome explicitly
  (`loading` / `loaded` / `unavailable`) so the UI can distinguish waiting from unknown.
- Fixtures live in `core/data/mock/` and must include honest gaps: a project without a version
  endpoint, a component that is not applicable, a measurement with no samples.

## Rendering rules

- Never fabricate a value. Render `Unknown`, `N/A`, `Not configured`, or an em dash via the
  `.co-unavailable` class.
- Status colour comes from `StatusLevel` and the `data-level` attribute; the wording comes from the
  contract label. Do not hardcode status colours in components.
- Production must never look like a local target: `co-environment-tag` emphasises production.
- Charts only when real samples exist. `co-sparkline` renders a dash for fewer than two samples.
- Format contract timestamps in UTC (`| date: 'hh:mm a' : 'UTC'`) so displayed times match the
  observation, not the browser's zone.
- V1 is read-only. Actions that belong to a later phase render as disabled with a title explaining
  why, rather than as buttons that do nothing.

## Icon system

`core/ui/icon.ts` owns the whole set as a `Record<IconName, IconDefinition>` of SVG path data on a
24px grid, 1.7 stroke, `currentColor`. Brand marks (`github`) are filled instead of stroked.

- Names are semantic (`rocket`, `database`, `ciCd`), never file paths, and the union is closed so
  templates type-check.
- Contracts stay semantic too: a contract says `workflow` or `versionSync`, and the component maps it
  to an `IconName`. Never put a rendering name in a contract.
- Colour comes from the surrounding CSS: navy/slate for structure, `--co-blue` for provider, action,
  and activity glyphs, status colours only for status.
- Add an icon only when a screen renders it, and prefer paths built from simple arcs and lines that
  stay legible at 15-17px. Avoid transplanting complex third-party path data.
- `co-project-mark` draws the hexagonal project glyph with the project initial and tone. Tone is
  presentation only and must never encode status.

## Design system

Tokens in `styles.scss`: warm off-white canvas, charcoal/slate ink, deep navy structure, `--co-blue`
accent, thin `--co-line` separators, serif display face for page titles, mono for commits. Status:
green healthy, blue running, amber warning, orange degraded, red down, grey unknown/N-A.

Shared primitives: `.co-eyebrow`, `.co-section-note`, `.co-table`, `.co-dot`, `.co-mono`,
`.co-unavailable`, `.co-inline-link`, `.co-section-footer`, `.co-sr-only`.

Avoid card grids, gradients, glassmorphism, heavy shadows, oversized icons, decorative charts, and
rainbow status colours. Prefer tables, thin rules, and generous spacing.

## Testing

- Vitest through `ng test`. The app is zoneless: always `await fixture.whenStable()` before asserting.
- Provide the port with the mock adapter in `TestBed.providers`; never reach into a real HTTP client.
- Cover what the product promises: correct facts rendered, honest unavailable states, environment
  scoping, and empty scopes. Do not test Angular itself.
