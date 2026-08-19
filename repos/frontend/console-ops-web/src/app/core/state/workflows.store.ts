import { DestroyRef, Injectable, computed, inject, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import {
  ManualRunSupport,
  WorkflowInventory,
  WorkflowRunHistory,
  WorkflowRunJob,
} from '../contracts/workflows';
import { WorkflowsDataSource } from '../data/workflows.data-source';

export type WorkflowsLoadState = 'loading' | 'loaded' | 'unavailable';

/**
 * Holds the workflow inventory for the Workflows screen.
 *
 * Re-reads do not blank the screen: only the first read shows a loading state, and a failed re-read keeps the
 * inventory already on screen rather than replacing a working page with an error.
 */
@Injectable({ providedIn: 'root' })
export class WorkflowsStore {
  private readonly dataSource = inject(WorkflowsDataSource);
  private readonly destroyRef = inject(DestroyRef);

  private readonly current = signal<WorkflowInventory | null>(null);
  private readonly state = signal<WorkflowsLoadState>('loading');

  readonly loadState = this.state.asReadonly();
  readonly groups = computed(() => this.current()?.groups ?? []);
  readonly readAt = computed(() => this.current()?.readAt ?? null);

  /** Whether what the screen is showing is sample data. Decided by the payload, never by the page. */
  readonly isSampleData = computed(() => this.current()?.isSampleData ?? false);

  readonly workflowCount = computed(() =>
    this.groups().reduce((total, group) => total + group.workflows.length, 0),
  );

  /**
   * Jobs of the selected workflow's latest run, keyed by run.
   *
   * Held here rather than in the page so a re-read of the inventory does not discard them, and so the same run
   * is not asked for twice while an operator moves between workflows.
   */
  private readonly jobsByRun = signal<Readonly<Record<string, readonly WorkflowRunJob[]>>>({});
  private readonly jobsLoading = signal<string | null>(null);
  private readonly jobsFailedFor = signal<string | null>(null);

  readonly jobsState = computed<'idle' | 'loading' | 'unavailable' | 'loaded'>(() => {
    const runId = this.selectedRunId();
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

  readonly selectedJobs = computed<readonly WorkflowRunJob[]>(() => {
    const runId = this.selectedRunId();
    return runId === null ? [] : (this.jobsByRun()[runId] ?? []);
  });

  private readonly selectedRunId = signal<string | null>(null);

  /**
   * Reads the jobs of one run once.
   *
   * A run already read is not asked for again: jobs of a finished run do not change, and a run still going is
   * re-read when the operator asks for it rather than on a timer.
   */
  readRunJobs(projectId: string, runId: string): void {
    this.selectedRunId.set(runId);

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
          // Named as unavailable rather than shown as a run with no jobs, which would be a different fact.
          this.jobsLoading.set(null);
          this.jobsFailedFor.set(runId);
        },
      });
  }

  /** Called when a workflow with no run is selected, so no stale job list is shown beside it. */
  clearRunJobs(): void {
    this.selectedRunId.set(null);
  }

  /**
   * Whether the selected workflow can be started manually, once its definition has been read.
   *
   * Keyed by workflow, because the answer is a property of the definition rather than of a run, and a definition
   * already read does not change while the screen is open.
   */
  private readonly manualRunByWorkflow = signal<Readonly<Record<string, ManualRunSupport>>>({});
  private readonly manualRunLoading = signal<string | null>(null);
  private readonly selectedWorkflowId = signal<string | null>(null);

  /**
   * The selected workflow's manual-run support: what the definition said, or `unknown` until it is read.
   *
   * Unknown covers both "not read yet" and "could not be established", because the screen says the same thing
   * for both and neither is a claim that a manual run is unavailable.
   */
  readonly selectedManualRun = computed<ManualRunSupport>(() => {
    const workflowId = this.selectedWorkflowId();
    return workflowId === null ? 'unknown' : (this.manualRunByWorkflow()[workflowId] ?? 'unknown');
  });

  readonly manualRunReading = computed(() => this.manualRunLoading() === this.selectedWorkflowId());

  /** Reads a workflow's dispatch support once. A failed read leaves it unknown rather than unavailable. */
  readManualRunSupport(projectId: string, workflowId: string, workflowPath: string): void {
    this.selectedWorkflowId.set(workflowId);

    if (
      untracked(this.manualRunByWorkflow)[workflowId] !== undefined ||
      untracked(this.manualRunLoading) === workflowId
    ) {
      return;
    }

    this.manualRunLoading.set(workflowId);

    this.dataSource
      .loadManualRunSupport(projectId, workflowId, workflowPath)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (reading) => {
          this.manualRunByWorkflow.update((current) => ({
            ...current,
            [workflowId]: reading.manualRun,
          }));
          this.manualRunLoading.set(null);
        },
        error: () => this.manualRunLoading.set(null),
      });
  }

  read(): void {
    if (untracked(this.state) !== 'loaded') {
      this.state.set('loading');
    }

    this.dataSource
      .load()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (inventory) => {
          this.current.set(inventory);
          this.state.set('loaded');
        },
        error: () => {
          if (untracked(this.current) === null) {
            this.state.set('unavailable');
          }
        },
      });
  }

  /** Whether any workflow on the page has a run that has not finished. */
  readonly hasRunningWorkflow = computed(() =>
    this.groups().some((group) =>
      group.workflows.some(
        (workflow) => workflow.latestRun !== null && workflow.latestRun.status !== 'completed',
      ),
    ),
  );

  /**
   * Re-reads what is moving.
   *
   * While a run is in progress it asks for that workflow's newest run only - one small request each - rather
   * than re-reading the whole inventory, which costs a request per workflow against a shared rate limit. With
   * nothing running there is nothing to follow, so it re-reads the inventory instead and picks up a workflow
   * that has started since.
   */
  refresh(): void {
    const running = this.groups().flatMap((group) =>
      group.workflows
        .filter(
          (workflow) => workflow.latestRun !== null && workflow.latestRun.status !== 'completed',
        )
        .map((workflow) => ({ projectId: group.projectId, workflowId: workflow.id })),
    );

    if (running.length === 0) {
      this.read();
      return;
    }

    for (const target of running) {
      this.dataSource
        .loadRuns(target.projectId, target.workflowId, 1)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: (history) => this.applyLatestRun(target.workflowId, history),
          // A failed refresh keeps what is on screen: it is a moment out of date, not wrong.
          error: () => undefined,
        });
    }

    const openRun = untracked(this.selectedRunId);
    if (openRun !== null) {
      this.refreshOpenRunJobs(openRun);
    }
  }

  /** Replaces one workflow's latest run in place, leaving the rest of the inventory as it was read. */
  private applyLatestRun(workflowId: string, history: WorkflowRunHistory): void {
    const latest = history.runs[0] ?? null;
    if (latest === null) {
      return;
    }

    this.current.update((inventory) =>
      inventory === null
        ? inventory
        : {
            ...inventory,
            groups: inventory.groups.map((group) => ({
              ...group,
              workflows: group.workflows.map((workflow) =>
                workflow.id === workflowId ? { ...workflow, latestRun: latest } : workflow,
              ),
            })),
          },
    );
  }

  /**
   * Re-reads the jobs of the open run while it is still going.
   *
   * The cache is deliberately bypassed here: jobs of a finished run do not change, but the jobs of a running one
   * are the thing an operator is watching, and serving them from a cache would freeze the screen.
   */
  private refreshOpenRunJobs(runId: string): void {
    const projectId = this.projectOfRun(runId);
    if (projectId === null) {
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

  private projectOfRun(runId: string): string | null {
    for (const group of this.groups()) {
      if (group.workflows.some((workflow) => workflow.latestRun?.id === runId)) {
        return group.projectId;
      }
    }

    return null;
  }
}
