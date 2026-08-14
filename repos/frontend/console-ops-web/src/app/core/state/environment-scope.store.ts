import { Injectable, signal } from '@angular/core';

import { EnvironmentKind } from '../contracts/dashboard-overview';

/** `null` scope means every environment is in view. */
export type EnvironmentScope = EnvironmentKind | null;

export interface EnvironmentScopeOption {
  readonly value: EnvironmentScope;
  readonly label: string;
}

export const ENVIRONMENT_SCOPE_OPTIONS: readonly EnvironmentScopeOption[] = [
  { value: null, label: 'All environments' },
  { value: 'production', label: 'Production' },
  { value: 'staging', label: 'Staging' },
  { value: 'development', label: 'Development' },
  { value: 'local', label: 'Local' },
];

/**
 * Which environment the operator is currently looking at.
 *
 * Held in a signal shared by the shell and the feature pages so the active scope is always explicit
 * and consistent, rather than each screen keeping its own idea of it.
 */
@Injectable({ providedIn: 'root' })
export class EnvironmentScopeStore {
  private readonly selected = signal<EnvironmentScope>(null);

  readonly scope = this.selected.asReadonly();

  select(scope: EnvironmentScope): void {
    this.selected.set(scope);
  }

  labelFor(scope: EnvironmentScope): string {
    return ENVIRONMENT_SCOPE_OPTIONS.find((option) => option.value === scope)?.label ?? 'Unknown';
  }
}
