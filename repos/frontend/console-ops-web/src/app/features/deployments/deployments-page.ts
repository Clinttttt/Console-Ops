import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';

import { DeploymentListItem } from '../../core/contracts/deployment-registry';
import { DeploymentRegistryStore } from '../../core/state/deployment-registry.store';
import { autoRefresh } from '../../core/state/auto-refresh';
import { EnvironmentScopeStore } from '../../core/state/environment-scope.store';
import { deploymentVerdict } from '../../core/ui/deployment-verdict';
import {
  DeploymentFilters,
  DeploymentProjectOption,
  DeploymentView,
} from './components/deployment-filters';
import { DeploymentTimeline } from './components/deployment-timeline';
import { DeploymentVerification } from './components/deployment-verification';
import { SelectedDeployment } from './components/selected-deployment';

/**
 * Deployments screen: release history and post-release verification.
 *
 * Read-only. Environment quick views write to the shared `EnvironmentScopeStore` so this screen agrees
 * with the shell selector, while the failed view is a verification filter held locally.
 *
 * Scoping by environment keeps only releases observed running in that environment. A release nobody
 * reported cannot be claimed for Production, so it drops out of that view rather than being assumed in.
 */
@Component({
  selector: 'co-deployments-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DeploymentFilters, DeploymentTimeline, DeploymentVerification, SelectedDeployment],
  templateUrl: './deployments-page.html',
  styleUrl: './deployments-page.scss',
})
export class DeploymentsPage {
  private readonly store = inject(DeploymentRegistryStore);
  private readonly environmentScope = inject(EnvironmentScopeStore);

  protected readonly loadState = this.store.loadState;
  protected readonly observedAt = this.store.observedAt;

  constructor() {
    // Releases are recorded by the API's scheduled sweeps; the timeline re-reads them as they arrive.
    autoRefresh(() => this.store.refresh());
  }

  protected readonly query = signal('');
  protected readonly projectId = signal<string | null>(null);
  protected readonly failedOnly = signal(false);
  private readonly selectedId = signal<string | null>(null);

  protected readonly projectOptions = computed<readonly DeploymentProjectOption[]>(() => {
    const seen = new Map<string, string>();
    for (const deployment of this.store.deployments()) {
      seen.set(deployment.projectId, deployment.projectName);
    }
    return [...seen]
      .map(([id, name]) => ({ id, name }))
      .sort((a, b) => a.name.localeCompare(b.name));
  });

  /** `null` when the current filters match no single quick view, such as a Development scope. */
  protected readonly activeView = computed<DeploymentView | null>(() => {
    if (this.failedOnly()) {
      return 'failed';
    }

    const scope = this.environmentScope.scope();
    if (scope === 'production' || scope === 'staging') {
      return scope;
    }

    return scope === null ? 'all' : null;
  });

  protected readonly deployments = computed<readonly DeploymentListItem[]>(() => {
    const scope = this.environmentScope.scope();
    const projectId = this.projectId();
    const query = this.query().trim().toLowerCase();
    const failedOnly = this.failedOnly();

    return this.store.deployments().filter((deployment) => {
      if (
        scope !== null &&
        !deployment.environments.some((observation) => observation.environment.kind === scope)
      ) {
        return false;
      }

      if (projectId !== null && deployment.projectId !== projectId) {
        return false;
      }

      if (failedOnly && deploymentVerdict(deployment).level !== 'down') {
        return false;
      }

      return query === '' || matchesQuery(deployment, query);
    });
  });

  protected readonly totalCount = computed(() => this.store.deployments().length);

  /** Falls back to the newest visible record so the rail never describes a hidden deployment. */
  protected readonly selected = computed<DeploymentListItem | null>(() => {
    const visible = [...this.deployments()].sort((left, right) =>
      right.deployedAt.localeCompare(left.deployedAt),
    );
    const selectedId = this.selectedId();
    return visible.find((deployment) => deployment.id === selectedId) ?? visible[0] ?? null;
  });

  protected select(deploymentId: string): void {
    this.selectedId.set(deploymentId);
  }

  protected setQuery(query: string): void {
    this.query.set(query);
  }

  protected setProject(projectId: string | null): void {
    this.projectId.set(projectId);
  }

  protected selectView(view: DeploymentView): void {
    this.failedOnly.set(view === 'failed');

    if (view === 'production' || view === 'staging') {
      this.environmentScope.select(view);
    } else if (view === 'all') {
      this.environmentScope.select(null);
    }
  }

  protected clearFilters(): void {
    this.query.set('');
    this.projectId.set(null);
    this.failedOnly.set(false);
    this.environmentScope.select(null);
  }

  protected retry(): void {
    this.store.refresh();
  }
}

function matchesQuery(deployment: DeploymentListItem, query: string): boolean {
  return (
    deployment.projectName.toLowerCase().includes(query) ||
    deployment.repository.toLowerCase().includes(query) ||
    deployment.branch.toLowerCase().includes(query) ||
    deployment.commitShortSha.toLowerCase().includes(query) ||
    (deployment.workflowFile?.toLowerCase().includes(query) ?? false) ||
    (deployment.triggeredBy?.toLowerCase().includes(query) ?? false) ||
    deployment.environments.some((observation) =>
      observation.environment.name.toLowerCase().includes(query),
    )
  );
}
