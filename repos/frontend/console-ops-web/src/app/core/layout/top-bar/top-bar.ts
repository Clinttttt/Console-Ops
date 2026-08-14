import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';

import {
  ENVIRONMENT_SCOPE_OPTIONS,
  EnvironmentScope,
  EnvironmentScopeStore,
} from '../../state/environment-scope.store';
import { Icon } from '../../ui/icon';

@Component({
  selector: 'co-top-bar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon],
  templateUrl: './top-bar.html',
  styleUrl: './top-bar.scss',
})
export class TopBar {
  private readonly environmentScope = inject(EnvironmentScopeStore);

  readonly title = input.required<string>();
  readonly subtitle = input<string | null>(null);

  protected readonly options = ENVIRONMENT_SCOPE_OPTIONS;
  protected readonly scope = this.environmentScope.scope;
  protected readonly scopeLabel = computed(() => this.environmentScope.labelFor(this.scope()));
  protected readonly scopeOpen = signal(false);

  /**
   * V1 is read-only, so notifications are declared but empty. The count comes from the activity
   * stream once meaningful operational events exist.
   */
  protected readonly notificationCount = 0;

  /** Operator identity arrives with authentication; until then it is shown as unresolved. */
  protected readonly operatorName = 'Local operator';
  protected readonly operatorInitials = 'LO';

  protected toggleScope(): void {
    this.scopeOpen.update((open) => !open);
  }

  protected selectScope(scope: EnvironmentScope): void {
    this.environmentScope.select(scope);
    this.scopeOpen.set(false);
  }
}
