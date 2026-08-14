import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';

import {
  EnvironmentScope,
  EnvironmentScopeStore,
} from '../../../core/state/environment-scope.store';
import { Icon } from '../../../core/ui/icon';

interface KindView {
  readonly value: EnvironmentScope;
  readonly label: string;
  readonly level: 'healthy' | 'running' | 'warning' | 'unknown';
}

/** Quick views mirror the environment kinds Console Ops recognises. */
const KIND_VIEWS: readonly KindView[] = [
  { value: null, label: 'All', level: 'unknown' },
  { value: 'production', label: 'Production', level: 'healthy' },
  { value: 'staging', label: 'Staging', level: 'running' },
  { value: 'development', label: 'Development', level: 'warning' },
  { value: 'local', label: 'Local', level: 'unknown' },
];

export interface ProjectFilterOption {
  readonly id: string;
  readonly name: string;
}

/**
 * Registry filters: text search, environment kind, and project.
 *
 * The kind buttons write to the shared `EnvironmentScopeStore`, the same signal the shell selector
 * uses, so the two controls can never disagree about the active environment.
 */
@Component({
  selector: 'co-environment-filters',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon],
  templateUrl: './environment-filters.html',
  styleUrl: './environment-filters.scss',
})
export class EnvironmentFilters {
  private readonly environmentScope = inject(EnvironmentScopeStore);

  readonly query = input('');
  readonly projects = input.required<readonly ProjectFilterOption[]>();
  readonly projectId = input<string | null>(null);
  /** `true` while the archived view is active, when no kind view is the current one. */
  readonly archivedActive = input(false);

  readonly queryChange = output<string>();
  readonly projectChange = output<string | null>();

  protected readonly views = KIND_VIEWS;
  protected readonly scope = this.environmentScope.scope;
  protected readonly activeScope = computed<EnvironmentScope | undefined>(() =>
    this.archivedActive() ? undefined : this.scope(),
  );

  protected selectKind(scope: EnvironmentScope): void {
    this.environmentScope.select(scope);
  }

  protected onProjectSelect(value: string): void {
    this.projectChange.emit(value === '' ? null : value);
  }
}
