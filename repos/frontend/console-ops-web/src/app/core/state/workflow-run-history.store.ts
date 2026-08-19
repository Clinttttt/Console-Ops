import { DestroyRef, Injectable, computed, inject, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { WorkflowRunHistory, WorkflowRunJob } from '../contracts/workflows';
import { WorkflowsDataSource } from '../data/workflows.data-source';

export type RunHistoryLoadState = 'loading' | 'loaded' | 'unavailable';

/**
 * Recent runs of one workflow, and the jobs of whichever run is open.
 *
 * Separate from the inventory store: this is read when an operator asks for one workflow's history, and holding
 * it beside the whole inventory would keep a page of runs alive for every workflow they ever looked at.
 */
@Injectable({ providedIn: 'root' })
export class WorkflowRunHistoryStore {
  private readonly dataSource = inject(WorkflowsDataSource);
  private readonly destroyRef = inject(DestroyRef);

  private readonly history = signal<WorkflowRunHistory | null>(null);
  private readonly state = signal<RunHistoryLoadState>('loading');
  private readonly jobsByRun = signal<Readonly<Record<string, readonly WorkflowRunJob[]>>>({});
  private readonly jobsLoading = signal<string | null>(null);
  private readonly jobsFailedFor = signal<string | null>(null);
  private readonly openRun = signal<string | null>(null);

  readonly loadState = this.state.asReadonly();
  readonly runs = computed(() => this.history()?.runs ?? []);
  readonly hasMore = computed(() => this.history()?.hasMore ?? false);
  readonly openRunId = this.openRun.asReadonly();

  readonly jobsState = computed<'idle' | 'loading' | 'unavailable' | 'loaded'>(() => {
    const runId = this.openRun();
    if (runId === null) {
      return 'idle';
    }

    if (this.jobsLoading() === runId) {
      return 'loading';
    }

    if (this.jobsFailedFor() === runId) {
      return 'unavailable';
    }

    return this.jobsByRun()[runId] === undefined ? 'idle' : 'loaded';
  });

  readonly openRunJobs = computed<readonly WorkflowRunJob[]>(() => {
    const runId = this.openRun();
    return runId === null ? [] : (this.jobsByRun()[runId] ?? []);
  });

  read(projectId: string, workflowId: string): void {
    this.state.set('loading');
    this.history.set(null);
    this.openRun.set(null);

    this.dataSource
      .loadRuns(projectId, workflowId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (history) => {
          this.history.set(history);
          this.state.set('loaded');
        },
        // Named as unavailable rather than shown as a workflow with no runs, which is a different fact.
        error: () => this.state.set('unavailable'),
      });
  }

  /** Whether any run in the list, or the open run, has not finished. */
  readonly hasRunningRun = computed(() => this.runs().some((run) => run.status !== 'completed'));

  /**
   * Re-reads the history, and the open run's jobs while that run is still going.
   *
   * Does not blank the screen or close what is open: a refresh that discarded the operator's place would make
   * watching a run worse than reloading the page. The jobs cache is bypassed for a running run, because its job
   * and step states are the thing being watched.
   */
  refresh(projectId: string, workflowId: string): void {
    this.dataSource
      .loadRuns(projectId, workflowId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (history) => this.history.set(history),
        // A failed refresh keeps what is on screen: it is a moment out of date, not wrong.
        error: () => undefined,
      });

    const runId = untracked(this.openRun);
    if (runId === null || !this.isRunning(runId)) {
      return;
    }

    this.dataSource
      .loadRunJobs(projectId, runId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (jobs) => this.jobsByRun.update((current) => ({ ...current, [runId]: jobs })),
        error: () => undefined,
      });
  }

  private isRunning(runId: string): boolean {
    return untracked(this.runs).some((run) => run.id === runId && run.status !== 'completed');
  }

  /** Opens a run's jobs, or closes it again. A run already read is not asked for twice. */
  toggleRun(projectId: string, runId: string): void {
    if (untracked(this.openRun) === runId) {
      this.openRun.set(null);
      return;
    }

    this.openRun.set(runId);
    if (untracked(this.jobsByRun)[runId] !== undefined || untracked(this.jobsLoading) === runId) {
      return;
    }

    this.jobsLoading.set(runId);
    this.jobsFailedFor.set(null);

    this.dataSource
      .loadRunJobs(projectId, runId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (jobs) => {
          this.jobsByRun.update((current) => ({ ...current, [runId]: jobs }));
          this.jobsLoading.set(null);
        },
        error: () => {
          this.jobsLoading.set(null);
          this.jobsFailedFor.set(runId);
        },
      });
  }
}
