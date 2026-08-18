import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { EnvironmentHealth, HealthCheckState } from '../../core/contracts/health';
import { autoRefresh } from '../../core/state/auto-refresh';
import { HealthStore } from '../../core/state/health.store';
import { healthCheckCell, isFailingState } from '../../core/ui/health-check-state';
import { Icon } from '../../core/ui/icon';
import { ProjectMark, ProjectMarkTone } from '../../core/ui/project-mark';
import { toneForProject } from '../../core/ui/project-tone';
import { Status } from '../../core/ui/status';
import { HealthRow } from './components/health-row';

/** Which environments the list is narrowed to. `null` is everything. */
type StateFilter = 'healthy' | 'failing' | 'unreachable' | null;

/**
 * Health.
 *
 * What is functioning right now, and what stopped. Every verdict is a recorded check: `Unknown` means no check
 * exists rather than "probably fine", and `Running` is not promoted to `Healthy` just because a process answers.
 *
 * Failing environments are lifted out into Active Issues rather than left to be found by scrolling, because the
 * question this screen exists to answer is "is anything wrong", and a list sorted by name answers it slowly.
 */
@Component({
  selector: 'co-health-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, HealthRow, Icon, ProjectMark, RouterLink, Status],
  templateUrl: './health-page.html',
  styleUrl: './health-page.scss',
})
export class HealthPage {
  private readonly store = inject(HealthStore);

  protected readonly loadState = this.store.loadState;
  protected readonly summary = this.store.summary;
  protected readonly stateChanges = this.store.stateChanges;
  protected readonly observedAt = this.store.observedAt;

  protected readonly stateFilter = signal<StateFilter>(null);
  private readonly selectedId = signal<string | null>(null);

  constructor() {
    this.store.read();

    // The API collects health on a schedule; a screen whose whole purpose is "what is functioning right now"
    // must re-read it rather than showing whatever was true when the page was opened.
    autoRefresh(() => this.store.read());
  }

  /** Environments needing attention, worst first. Kept separate from the list, not duplicated out of it. */
  protected readonly activeIssues = computed(() =>
    this.store.environments().filter((environment) => isFailingState(environment.state)),
  );

  protected readonly environments = computed(() => {
    const filter = this.stateFilter();
    return this.store
      .environments()
      .filter((environment) => this.matches(environment.state, filter));
  });

  /**
   * The environment in the rail. Nothing is selected until a row is chosen, because a panel describing an
   * arbitrary environment is worse than an empty one.
   */
  protected readonly selected = computed<EnvironmentHealth | null>(() => {
    const id = this.selectedId();
    return id === null
      ? null
      : (this.store.environments().find((environment) => environment.id === id) ?? null);
  });

  protected readonly selectedVerdict = computed(() => {
    const environment = this.selected();
    return environment === null ? null : healthCheckCell(environment.state);
  });

  protected readonly selectedChecks = computed(() =>
    (this.selected()?.checks ?? []).map((check) => ({
      ...check,
      cell: healthCheckCell(check.state),
    })),
  );

  protected select(environmentId: string): void {
    this.selectedId.update((current) => (current === environmentId ? null : environmentId));
  }

  protected setStateFilter(filter: StateFilter): void {
    this.stateFilter.set(filter);
    this.selectedId.set(null);
  }

  protected toneFor(projectId: string): ProjectMarkTone {
    return toneForProject(projectId);
  }

  protected checkCell(state: HealthCheckState) {
    return healthCheckCell(state);
  }

  /** How long a failing run has lasted, from the recorded first failure. */
  protected failingFor(environment: EnvironmentHealth): string | null {
    const observedAt = this.observedAt();
    if (environment.failingSince === null || observedAt === null) {
      return null;
    }

    return duration(Date.parse(observedAt) - Date.parse(environment.failingSince));
  }

  /** How long the current healthy run has held. */
  protected healthyFor(environment: EnvironmentHealth): string | null {
    const observedAt = this.observedAt();
    if (environment.healthySince === null || observedAt === null) {
      return null;
    }

    return duration(Date.parse(observedAt) - Date.parse(environment.healthySince));
  }

  private matches(state: HealthCheckState, filter: StateFilter): boolean {
    switch (filter) {
      case null:
        return true;
      case 'healthy':
        return state === 'healthy' || state === 'running';
      case 'failing':
        return state === 'degraded' || state === 'unhealthy';
      default:
        return state === 'unreachable' || state === 'unknown';
    }
  }
}

/** Coarse and honest: an operator reading a failure wants its scale, not its milliseconds. */
function duration(milliseconds: number): string {
  const seconds = Math.max(0, Math.round(milliseconds / 1000));
  if (seconds < 60) {
    return `${seconds}s`;
  }

  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) {
    return `${minutes}m ${seconds % 60}s`;
  }

  const hours = Math.floor(minutes / 60);
  return hours < 24 ? `${hours}h ${minutes % 60}m` : `${Math.floor(hours / 24)}d ${hours % 24}h`;
}
