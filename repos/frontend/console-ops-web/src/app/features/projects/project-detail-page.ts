import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { rxResource, takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { map } from 'rxjs';

import { ProjectSurface } from '../../core/contracts/dashboard-overview';
import { ProjectEnvironmentRef, ProjectListItem } from '../../core/contracts/project-registry';
import { ProjectRegistryDataSource } from '../../core/data/project-registry.data-source';
import { autoRefresh } from '../../core/state/auto-refresh';
import { DashboardOverviewStore } from '../../core/state/dashboard-overview.store';
import { ProjectMark, ProjectMarkTone } from '../../core/ui/project-mark';
import { toneForProject } from '../../core/ui/project-tone';
import { ProjectEnvironmentPanel } from './components/project-environment-panel';
import { ProjectSourcePanel } from './components/project-source-panel';

type LoadState = 'loading' | 'loaded' | 'notFound' | 'unavailable';

/** One environment's configuration paired with whatever has been observed about it. */
export interface EnvironmentView {
  readonly configured: ProjectEnvironmentRef;
  /** `null` until a refresh has observed this environment. */
  readonly observed: ProjectSurface | null;
}

/**
 * Project detail: everything Console Ops holds about one project.
 *
 * Configuration comes from the project resource; live state comes from the stored observations in the
 * dashboard overview. An environment with no observation says so rather than borrowing another's.
 */
@Component({
  selector: 'co-project-detail-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, ProjectEnvironmentPanel, ProjectMark, ProjectSourcePanel, RouterLink],
  templateUrl: './project-detail-page.html',
  styleUrl: './project-detail-page.scss',
})
export class ProjectDetailPage {
  private readonly projects = inject(ProjectRegistryDataSource);
  private readonly dashboard = inject(DashboardOverviewStore);
  private readonly destroyRef = inject(DestroyRef);

  /**
   * Taken from the route rather than a required input: the resource's parameters are evaluated before
   * component inputs are guaranteed to be bound, and reading a required input then throws.
   */
  protected readonly projectId = toSignal(
    inject(ActivatedRoute).paramMap.pipe(map((params) => params.get('projectId') ?? '')),
    { initialValue: '' },
  );

  private readonly resource = rxResource({
    params: () => this.projectId() || undefined,
    stream: ({ params }) => this.projects.getProject(params),
  });

  protected readonly project = computed<ProjectListItem | null>(
    () => this.resource.value() ?? null,
  );

  protected readonly loadState = computed<LoadState>(() => {
    if (this.resource.isLoading()) {
      return 'loading';
    }

    const error = this.resource.error();
    if (error !== undefined) {
      return error instanceof HttpErrorResponse && error.status === 404
        ? 'notFound'
        : 'unavailable';
    }

    return this.resource.value() === undefined ? 'loading' : 'loaded';
  });

  protected readonly refreshing = signal(false);
  protected readonly refreshError = signal<string | null>(null);

  protected readonly tone = computed<ProjectMarkTone>(() => toneForProject(this.projectId()));

  /** Observed surfaces for this project, keyed by environment id. */
  private readonly surfaces = computed(() => {
    const map = new Map<string, ProjectSurface>();
    for (const surface of this.dashboard.overview()?.projects ?? []) {
      if (surface.id === this.projectId()) {
        map.set(surface.environment.id, surface);
      }
    }
    return map;
  });

  protected readonly environments = computed<readonly EnvironmentView[]>(() =>
    (this.project()?.environments ?? []).map((configured) => ({
      configured,
      observed: this.surfaces().get(configured.id) ?? null,
    })),
  );

  /** Any surface for this project carries its source and workflow facts. */
  protected readonly sourceSurface = computed<ProjectSurface | null>(
    () => this.environments().find((view) => view.observed !== null)?.observed ?? null,
  );

  constructor() {
    // Re-reads what has been recorded. "Refresh observations" below is the way to collect right now.
    autoRefresh(() => this.dashboard.refresh());
  }

  protected refresh(): void {
    if (this.refreshing()) {
      return;
    }

    this.refreshing.set(true);
    this.refreshError.set(null);

    this.projects
      .refreshProject(this.projectId())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          // The overview holds the observations this page displays, so it is reloaded too.
          this.dashboard.refresh();
          this.refreshing.set(false);
        },
        error: (error: unknown) => {
          this.refreshError.set(refreshErrorMessage(error));
          this.refreshing.set(false);
        },
      });
  }
}

function refreshErrorMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse && error.status === 0) {
    return 'The Console Ops API is unavailable, so nothing could be refreshed.';
  }
  return 'The refresh could not be completed. Try again.';
}
