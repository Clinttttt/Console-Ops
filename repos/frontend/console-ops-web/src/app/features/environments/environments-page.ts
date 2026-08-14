import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';

import { EnvironmentListItem, StatusCell } from '../../core/contracts/environment-registry';
import { EnvironmentRegistryStore } from '../../core/state/environment-registry.store';
import { EnvironmentScopeStore } from '../../core/state/environment-scope.store';
import { EnvironmentFilters, ProjectFilterOption } from './components/environment-filters';
import { EnvironmentGroups } from './components/environment-groups';
import { SelectedEnvironment } from './components/selected-environment';

/**
 * Environments screen: runtime targets and configuration across every registered project.
 *
 * Read-only. The environment kind filter is the shared `EnvironmentScopeStore`, so this screen, the
 * shell selector, and the Projects quick views always agree on the active environment.
 */
@Component({
  selector: 'co-environments-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [EnvironmentFilters, EnvironmentGroups, SelectedEnvironment],
  templateUrl: './environments-page.html',
  styleUrl: './environments-page.scss',
})
export class EnvironmentsPage {
  private readonly store = inject(EnvironmentRegistryStore);
  private readonly environmentScope = inject(EnvironmentScopeStore);

  protected readonly loadState = this.store.loadState;
  protected readonly observedAt = this.store.observedAt;

  protected readonly query = signal('');
  protected readonly projectId = signal<string | null>(null);
  protected readonly showArchived = signal(false);
  private readonly selectedId = signal<string | null>(null);

  /** Only projects reachable in the current view, so the filter can never offer an empty result. */
  protected readonly projectOptions = computed<readonly ProjectFilterOption[]>(() => {
    const seen = new Map<string, string>();
    for (const environment of this.inLifecycle()) {
      seen.set(environment.projectId, environment.projectName);
    }
    return [...seen]
      .map(([id, name]) => ({ id, name }))
      .sort((a, b) => a.name.localeCompare(b.name));
  });

  /**
   * Everything in the current lifecycle view, before the kind, project, and text filters.
   * The distribution describes the whole registry, so it counts this set rather than the filtered one.
   */
  protected readonly inLifecycle = computed(() => {
    const lifecycle = this.showArchived() ? 'archived' : 'active';
    return this.store.environments().filter((item) => item.lifecycle === lifecycle);
  });

  protected readonly environments = computed<readonly EnvironmentListItem[]>(() => {
    const scope = this.environmentScope.scope();
    const projectId = this.projectId();
    const query = this.query().trim().toLowerCase();

    return this.inLifecycle().filter((environment) => {
      if (scope !== null && environment.kind !== scope) {
        return false;
      }

      if (projectId !== null && environment.projectId !== projectId) {
        return false;
      }

      return query === '' || matchesQuery(environment, query);
    });
  });

  protected readonly totalCount = computed(() => this.inLifecycle().length);

  /**
   * Nothing is selected until the operator picks a row, so the screen never highlights a card the
   * operator did not choose. A selection that scrolls out of the current filters resolves to `null`.
   */
  protected readonly selected = computed<EnvironmentListItem | null>(() => {
    const selectedId = this.selectedId();
    if (selectedId === null) {
      return null;
    }

    return this.environments().find((environment) => environment.id === selectedId) ?? null;
  });

  protected readonly selectedVersionSync = computed<StatusCell | null>(() => {
    const selected = this.selected();
    if (selected === null) {
      return null;
    }

    const { state, deployedCommitShortSha } = selected.versionSync;
    switch (state) {
      case 'inSync':
        return { level: 'healthy', label: 'In Sync', detail: deployedCommitShortSha };
      case 'behind':
        return { level: 'warning', label: 'Behind', detail: deployedCommitShortSha };
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

  protected showArchivedEnvironments(): void {
    this.showArchived.set(true);
  }

  protected clearFilters(): void {
    this.query.set('');
    this.projectId.set(null);
    this.showArchived.set(false);
    this.environmentScope.select(null);
  }

  protected retry(): void {
    this.store.refresh();
  }
}

function matchesQuery(environment: EnvironmentListItem, query: string): boolean {
  return (
    environment.projectName.toLowerCase().includes(query) ||
    environment.name.toLowerCase().includes(query) ||
    (environment.runtime?.target?.toLowerCase().includes(query) ?? false) ||
    (environment.applicationUrl?.toLowerCase().includes(query) ?? false)
  );
}
