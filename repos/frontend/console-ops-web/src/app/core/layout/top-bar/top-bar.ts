import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';

import {
  ENVIRONMENT_SCOPE_OPTIONS,
  EnvironmentScope,
  EnvironmentScopeStore,
} from '../../state/environment-scope.store';
import { SessionStore } from '../../state/session.store';
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
  private readonly sessions = inject(SessionStore);

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

  protected readonly operator = this.sessions.operator;
  protected readonly isSignedIn = this.sessions.isSignedIn;
  protected readonly menuOpen = signal(false);

  /**
   * The GitHub login, or a plain statement that nobody is signed in.
   *
   * Not a placeholder name. This is the account a run is recorded against, so a label that said "Local operator"
   * while GitHub attributed the run to somebody else was describing a person who does not exist.
   */
  protected readonly operatorName = computed(() => this.operator()?.login ?? 'Not signed in');

  /** Initials are only a fallback for an account GitHub gave no avatar for. */
  protected readonly operatorInitials = computed(() => {
    const login = this.operator()?.login;
    if (login === undefined || login.length === 0) {
      return '--';
    }

    const parts = login.split(/[-_.]/).filter((part) => part.length > 0);
    return (parts.length > 1 ? `${parts[0][0]}${parts[1][0]}` : login.slice(0, 2)).toUpperCase();
  });

  protected readonly avatarUrl = computed(() => this.operator()?.avatarUrl ?? null);

  protected toggleMenu(): void {
    this.menuOpen.update((open) => !open);
  }

  protected signOut(): void {
    this.menuOpen.set(false);
    this.sessions.signOut();
  }

  protected toggleScope(): void {
    this.scopeOpen.update((open) => !open);
  }

  protected selectScope(scope: EnvironmentScope): void {
    this.environmentScope.select(scope);
    this.scopeOpen.set(false);
  }
}
