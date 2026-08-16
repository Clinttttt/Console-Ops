import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, tap } from 'rxjs';

import { ProjectRegistrationRequest } from '../contracts/project-registration';
import { ProjectUpdateRequest } from '../contracts/project-update';
import { ProjectListItem, ProjectRegistry } from '../contracts/project-registry';
import { ProjectRegistryDataSource } from '../data/project-registry.data-source';

/** Load outcome, kept explicit so the UI can distinguish "waiting" from "could not be determined". */
export type ProjectRegistryLoadState = 'loading' | 'loaded' | 'unavailable';

/** Holds the registered projects for the Projects screen. */
@Injectable({ providedIn: 'root' })
export class ProjectRegistryStore {
  private readonly dataSource = inject(ProjectRegistryDataSource);
  private readonly destroyRef = inject(DestroyRef);

  private readonly current = signal<ProjectRegistry>([]);
  private readonly state = signal<ProjectRegistryLoadState>('loading');

  readonly registry = this.current.asReadonly();
  readonly loadState = this.state.asReadonly();

  readonly projects = computed<readonly ProjectListItem[]>(() => this.current());

  /** Newest registrations first. Recency comes from the data, never from list order. */
  readonly recentlyAdded = computed(() =>
    [...this.projects()]
      .sort((left, right) => right.createdAtUtc.localeCompare(left.createdAtUtc))
      .slice(0, 3),
  );

  constructor() {
    this.refresh();
  }

  /**
   * Reads the registry.
   *
   * Only the first read shows a loading state, so a scheduled re-read cannot blank the screen, and a
   * failed re-read leaves the last known projects in place rather than emptying the list.
   */
  refresh(): void {
    const isFirstRead = this.state() !== 'loaded';
    if (isFirstRead) {
      this.state.set('loading');
    }

    this.dataSource
      .load()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (registry) => {
          this.current.set(registry);
          this.state.set('loaded');
        },
        error: () => {
          if (isFirstRead) {
            this.current.set([]);
            this.state.set('unavailable');
          }
        },
      });
  }

  register(request: ProjectRegistrationRequest): Observable<ProjectListItem> {
    return this.dataSource.register(request).pipe(
      tap((project) => {
        this.current.update((projects) => [
          project,
          ...projects.filter(({ id }) => id !== project.id),
        ]);
        this.state.set('loaded');
      }),
    );
  }

  /** Replaces the stored project on success, so lists reflect the edit without a reload. */
  updateProject(projectId: string, request: ProjectUpdateRequest): Observable<ProjectListItem> {
    return this.dataSource.updateProject(projectId, request).pipe(
      tap((project) => {
        this.current.update((projects) =>
          projects.map((existing) => (existing.id === project.id ? project : existing)),
        );
        this.state.set('loaded');
      }),
    );
  }

  /** Archived projects leave normal queries, so the local list drops it too. */
  archiveProject(projectId: string): Observable<unknown> {
    return this.dataSource
      .archiveProject(projectId)
      .pipe(
        tap(() =>
          this.current.update((projects) =>
            projects.filter((existing) => existing.id !== projectId),
          ),
        ),
      );
  }

  refreshProject(projectId: string): Observable<unknown> {
    return this.dataSource.refreshProject(projectId);
  }
}
