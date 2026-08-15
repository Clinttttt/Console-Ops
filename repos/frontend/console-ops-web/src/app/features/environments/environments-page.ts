import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';

import {
  EnvironmentKind,
  ProjectSurface,
  StatusCell,
} from '../../core/contracts/dashboard-overview';
import { DashboardOverviewStore } from '../../core/state/dashboard-overview.store';
import { EnvironmentScopeStore } from '../../core/state/environment-scope.store';
import { ProjectRegistryStore } from '../../core/state/project-registry.store';
import { EnvironmentFilters, ProjectFilterOption } from './components/environment-filters';
import { EnvironmentGroups } from './components/environment-groups';
import { SelectedEnvironment } from './components/selected-environment';

/**
 * One configured environment, paired with whatever has been observed about it.
 *
 * Configuration comes from the project resource; observations come from the stored dashboard overview.
 * `observed` is `null` until a refresh has looked at this environment.
 */
export interface EnvironmentRow {
  readonly id: string;
  readonly projectId: string;
  readonly projectName: string;
  readonly name: string;
  readonly kind: EnvironmentKind;
  readonly applicationUrl: string | null;
  readonly healthUrl: string | null;
  readonly versionUrl: string | null;
  readonly observed: ProjectSurface | null;
}

/**
 * Environments screen: every environment Console Ops has been told about, and its current state.
 *
 * Built entirely from V1 facts. Runtime provider, revisions, configuration completeness and deployment
 * history are later phases, so they are absent rather than guessed at.
 */
@Component({
  selector: 'co-environments-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [EnvironmentFilters, EnvironmentGroups, SelectedEnvironment],
  templateUrl: './environments-page.html',
  styleUrl: './environments-page.scss',
})
export class EnvironmentsPage {
  private readonly projects = inject(ProjectRegistryStore);
  private readonly dashboard = inject(DashboardOverviewStore);
  private readonly environmentScope = inject(EnvironmentScopeStore);

  protected readonly query = signal('');
  protected readonly projectId = signal<string | null>(null);
  private readonly selectedId = signal<string | null>(null);

  /** The registry decides whether this screen has anything to show; observations are additive. */
  protected readonly loadState = this.projects.loadState;

  /** Observed surfaces keyed by environment id, so each environment reads only its own. */
  private readonly surfaces = computed(() => {
    const map = new Map<string, ProjectSurface>();
    for (const surface of this.dashboard.overview()?.projects ?? []) {
      map.set(surface.environment.id, surface);
    }
    return map;
  });

  private readonly allRows = computed<readonly EnvironmentRow[]>(() =>
    this.projects.projects().flatMap((project) =>
      project.environments.map((environment) => ({
        id: environment.id,
        projectId: project.id,
        projectName: project.name,
        name: environment.name,
        kind: environment.kind,
        applicationUrl: environment.applicationUrl,
        healthUrl: environment.healthUrl,
        versionUrl: environment.versionUrl,
        observed: this.surfaces().get(environment.id) ?? null,
      })),
    ),
  );

  protected readonly totalCount = computed(() => this.allRows().length);

  protected readonly projectOptions = computed<readonly ProjectFilterOption[]>(() => {
    const seen = new Map<string, string>();
    for (const row of this.allRows()) {
      seen.set(row.projectId, row.projectName);
    }
    return [...seen]
      .map(([id, name]) => ({ id, name }))
      .sort((a, b) => a.name.localeCompare(b.name));
  });

  protected readonly environments = computed<readonly EnvironmentRow[]>(() => {
    const scope = this.environmentScope.scope();
    const projectId = this.projectId();
    const query = this.query().trim().toLowerCase();

    return this.allRows().filter((row) => {
      if (scope !== null && row.kind !== scope) {
        return false;
      }

      if (projectId !== null && row.projectId !== projectId) {
        return false;
      }

      return query === '' || matchesQuery(row, query);
    });
  });

  /** Nothing is selected until the operator picks a row, and a filtered-out selection clears. */
  protected readonly selected = computed<EnvironmentRow | null>(() => {
    const selectedId = this.selectedId();
    if (selectedId === null) {
      return null;
    }

    return this.environments().find((row) => row.id === selectedId) ?? null;
  });

  protected readonly selectedVersionSync = computed<StatusCell | null>(() => {
    const observed = this.selected()?.observed ?? null;
    if (observed === null) {
      return null;
    }

    const { state, commitsBehind } = observed.versionSync;
    switch (state) {
      case 'inSync':
        return { level: 'healthy', label: 'In Sync', detail: null };
      case 'behind':
        return {
          level: 'warning',
          label: 'Behind',
          detail: commitsBehind === null ? null : `${commitsBehind} commits`,
        };
      case 'notConfigured':
        return { level: 'notApplicable', label: 'Not configured', detail: null };
      default:
        return { level: 'unknown', label: 'Unknown', detail: null };
    }
  });

  protected select(environmentId: string): void {
    this.selectedId.set(environmentId);
  }

  protected setQuery(query: string): void {
    this.query.set(query);
  }

  protected setProject(projectId: string | null): void {
    this.projectId.set(projectId);
  }

  protected clearFilters(): void {
    this.query.set('');
    this.projectId.set(null);
    this.environmentScope.select(null);
  }

  protected retry(): void {
    this.projects.refresh();
  }
}

function matchesQuery(row: EnvironmentRow, query: string): boolean {
  return (
    row.projectName.toLowerCase().includes(query) ||
    row.name.toLowerCase().includes(query) ||
    (row.applicationUrl?.toLowerCase().includes(query) ?? false)
  );
}
