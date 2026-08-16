import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';

import { SystemStateMatrix } from '../../core/contracts/dashboard-overview';
import { autoRefresh } from '../../core/state/auto-refresh';
import { DashboardOverviewStore } from '../../core/state/dashboard-overview.store';
import { EnvironmentScopeStore } from '../../core/state/environment-scope.store';
import { PipelineFlowSection } from './components/pipeline-flow';
import { ProjectSurfacesSection } from './components/project-surfaces';
import { RecentActivitySection } from './components/recent-activity';
import { SystemStateSection } from './components/system-state';

/**
 * Overview (home) screen: rapid operational awareness across every registered project.
 *
 * Read-only by design for V1. Everything shown comes from the dashboard overview contract, and any
 * fact the platform could not establish is rendered as unavailable rather than filled in.
 */
@Component({
  selector: 'co-overview-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PipelineFlowSection, ProjectSurfacesSection, SystemStateSection, RecentActivitySection],
  templateUrl: './overview-page.html',
  styleUrl: './overview-page.scss',
})
export class OverviewPage {
  private readonly store = inject(DashboardOverviewStore);
  private readonly environmentScope = inject(EnvironmentScopeStore);

  protected readonly loadState = this.store.loadState;
  protected readonly overview = this.store.overview;

  constructor() {
    // The API collects observations on a schedule; the screen re-reads them so it stays current.
    autoRefresh(() => this.store.refresh());
  }
  protected readonly scopeLabel = computed(() =>
    this.environmentScope.labelFor(this.environmentScope.scope()),
  );

  protected readonly projects = computed(() => {
    const projects = this.overview()?.projects ?? [];
    const scope = this.environmentScope.scope();
    return scope === null
      ? projects
      : projects.filter((project) => project.environment.kind === scope);
  });

  /** The matrix is narrowed to the projects in scope so its columns always match the table above. */
  protected readonly systemState = computed<SystemStateMatrix | null>(() => {
    const matrix = this.overview()?.systemState ?? null;
    if (matrix === null) {
      return null;
    }

    const visible = new Set(this.projects().map((project) => project.environment.id));
    const keptIndexes = matrix.columns
      .map((column, index) => ({ column, index }))
      .filter(({ column }) => visible.has(column.environmentId));

    return {
      columns: keptIndexes.map(({ column }) => column),
      rows: matrix.rows.map((row) => ({
        ...row,
        cells: keptIndexes.map(({ index }) => row.cells[index] ?? null),
      })),
    };
  });

  protected retry(): void {
    this.store.refresh();
  }
}
