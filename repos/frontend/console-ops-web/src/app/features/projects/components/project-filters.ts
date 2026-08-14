import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';

import {
  ENVIRONMENT_SCOPE_OPTIONS,
  EnvironmentScope,
  EnvironmentScopeStore,
} from '../../../core/state/environment-scope.store';
import { Icon } from '../../../core/ui/icon';

/** Quick views over the registry. `null` means the current filters match no single quick view. */
export type RegistryView = 'all' | 'production' | 'local' | 'archived';

interface ViewOption {
  readonly value: RegistryView;
  readonly label: string;
}

const VIEWS: readonly ViewOption[] = [
  { value: 'all', label: 'All' },
  { value: 'production', label: 'Production' },
  { value: 'local', label: 'Local' },
  { value: 'archived', label: 'Archived' },
];

/**
 * Registry filters: text search, quick views, and environment scope.
 *
 * The environment select writes to the shared `EnvironmentScopeStore`, the same signal the shell's
 * selector uses, so the two controls can never disagree about what the operator is looking at.
 */
@Component({
  selector: 'co-project-filters',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon, RouterLink],
  templateUrl: './project-filters.html',
  styleUrl: './project-filters.scss',
})
export class ProjectFilters {
  private readonly environmentScope = inject(EnvironmentScopeStore);

  readonly query = input('');
  readonly activeView = input.required<RegistryView | null>();

  readonly queryChange = output<string>();
  readonly viewChange = output<RegistryView>();

  protected readonly views = VIEWS;
  protected readonly scopeOptions = ENVIRONMENT_SCOPE_OPTIONS;
  protected readonly scope = this.environmentScope.scope;
  protected readonly scopeLabel = computed(() => this.environmentScope.labelFor(this.scope()));

  protected onQueryInput(value: string): void {
    this.queryChange.emit(value);
  }

  protected onScopeSelect(value: string): void {
    const scope = (value === '' ? null : value) as EnvironmentScope;
    this.environmentScope.select(scope);
  }
}
