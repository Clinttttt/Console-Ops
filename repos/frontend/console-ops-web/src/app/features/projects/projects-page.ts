import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';

import { ProjectListItem } from '../../core/contracts/project-registry';
import { EnvironmentScopeStore } from '../../core/state/environment-scope.store';
import { ProjectRegistryStore } from '../../core/state/project-registry.store';
import { ProjectFilters, RegistryView } from './components/project-filters';
import { ProjectIntake } from './components/project-intake';
import { ProjectRegistryTable } from './components/project-registry-table';

/**
 * Projects screen: the registry of everything Console Ops monitors.
 *
 * Read-only for now. The active view is derived from the environment scope and the archived toggle
 * rather than stored separately, so the quick views, the toolbar select, and the shell selector can
 * never disagree.
 */
@Component({
  selector: 'co-projects-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ProjectFilters, ProjectIntake, ProjectRegistryTable],
  templateUrl: './projects-page.html',
  styleUrl: './projects-page.scss',
})
export class ProjectsPage {
  private readonly store = inject(ProjectRegistryStore);
  private readonly environmentScope = inject(EnvironmentScopeStore);

  protected readonly loadState = this.store.loadState;
  protected readonly recentlyAdded = this.store.recentlyAdded;

  protected readonly query = signal('');
  protected readonly showArchived = signal(false);

  /** `null` when the scope is one the quick views do not cover, such as Staging. */
  protected readonly activeView = computed<RegistryView | null>(() => {
    if (this.showArchived()) {
      return 'archived';
    }

    const scope = this.environmentScope.scope();
    if (scope === 'production' || scope === 'local') {
      return scope;
    }

    return scope === null ? 'all' : null;
  });

  protected readonly projects = computed<readonly ProjectListItem[]>(() => {
    const lifecycle = this.showArchived() ? 'archived' : 'active';
    const scope = this.environmentScope.scope();
    const query = this.query().trim().toLowerCase();

    return this.store.projects().filter((project) => {
      if (project.lifecycle !== lifecycle) {
        return false;
      }

      if (scope !== null && project.environment.kind !== scope) {
        return false;
      }

      return query === '' || matchesQuery(project, query);
    });
  });

  protected setQuery(query: string): void {
    this.query.set(query);
  }

  protected selectView(view: RegistryView): void {
    this.showArchived.set(view === 'archived');

    if (view === 'production' || view === 'local') {
      this.environmentScope.select(view);
    } else if (view === 'all') {
      this.environmentScope.select(null);
    }
  }

  protected clearFilters(): void {
    this.query.set('');
    this.showArchived.set(false);
    this.environmentScope.select(null);
  }

  protected retry(): void {
    this.store.refresh();
  }
}

function matchesQuery(project: ProjectListItem, query: string): boolean {
  return (
    project.name.toLowerCase().includes(query) ||
    project.repository.repository.toLowerCase().includes(query) ||
    (project.description?.toLowerCase().includes(query) ?? false)
  );
}
