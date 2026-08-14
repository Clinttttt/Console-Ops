import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';

import { EnvironmentScopeStore } from '../../../core/state/environment-scope.store';
import { Icon } from '../../../core/ui/icon';

/**
 * Quick views over deployment history.
 *
 * `production` and `staging` scope by environment and are held in the shared `EnvironmentScopeStore`.
 * `failed` is a verification filter, so it is a separate view rather than an environment.
 */
export type DeploymentView = 'all' | 'production' | 'staging' | 'failed';

interface ViewOption {
  readonly value: DeploymentView;
  readonly label: string;
  readonly level: 'healthy' | 'running' | 'down' | null;
}

const VIEWS: readonly ViewOption[] = [
  { value: 'all', label: 'All', level: null },
  { value: 'production', label: 'Production', level: 'healthy' },
  { value: 'staging', label: 'Staging', level: 'running' },
  { value: 'failed', label: 'Failed', level: 'down' },
];

export interface DeploymentProjectOption {
  readonly id: string;
  readonly name: string;
}

@Component({
  selector: 'co-deployment-filters',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon],
  templateUrl: './deployment-filters.html',
  styleUrl: './deployment-filters.scss',
})
export class DeploymentFilters {
  private readonly environmentScope = inject(EnvironmentScopeStore);

  readonly query = input('');
  readonly projects = input.required<readonly DeploymentProjectOption[]>();
  readonly projectId = input<string | null>(null);
  readonly activeView = input.required<DeploymentView | null>();

  readonly queryChange = output<string>();
  readonly projectChange = output<string | null>();
  readonly viewChange = output<DeploymentView>();

  protected readonly views = VIEWS;
  protected readonly scope = this.environmentScope.scope;

  protected onProjectSelect(value: string): void {
    this.projectChange.emit(value === '' ? null : value);
  }
}
