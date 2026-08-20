import { DestroyRef, Injectable, computed, inject, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import {
  ManualRunSupport,
  WorkflowInventory,
  WorkflowRunHistory,
  WorkflowRunJob,
  WorkflowRiskLevel,
  Workflow,
  WorkflowInput,
  WorkflowBranches,
  WorkflowRiskReading,
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
  private readonly inputsByWorkflow = signal<Readonly<Record<string, readonly WorkflowInput[]>>>(
    {},
  );
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

  /** What the selected workflow declares it needs for a run, once its definition has been read. */
  readonly selectedInputs = computed<readonly WorkflowInput[]>(() => {
    const workflowId = this.selectedWorkflowId();
    return workflowId === null ? [] : (this.inputsByWorkflow()[workflowId] ?? []);
  });

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
          this.inputsByWorkflow.update((current) => ({ ...current, [workflowId]: reading.inputs }));
          this.manualRunLoading.set(null);
        },
        error: () => this.manualRunLoading.set(null),
      });
  }

  /**
   * Records a workflow's risk and shows it straight away.
   *
   * The marking is applied in place from what the API returned, then the inventory is re-read in the background to
   * reconcile. Waiting for that read before releasing the control left the choice unclickable for seconds while
   * the previous outcome sat on screen - the marking looked rejected and then corrected itself.
   *
   * `executable` is recomputed here with the rule the API applies - active, and marked - rather than guessed at.
   * The two agree because the rule is one line; the re-read is what proves it.
   */
  setRisk(projectId: string, workflowPath: string, level: WorkflowRiskLevel): void {
    this.riskSaving.set(workflowPath);
    this.riskError.set(null);

    this.dataSource
      .setRisk(projectId, workflowPath, level)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (marking) => {
          this.applyRisk(workflowPath, marking);
          this.riskSaving.set(null);
          this.read();
        },
        error: () => {
          // Named, because a marking that silently failed to save would leave an operator believing a
          // destructive workflow was marked as such.
          this.riskSaving.set(null);
          this.riskError.set('That risk marking could not be saved.');
        },
      });
  }

  private applyRisk(workflowPath: string, marking: WorkflowRiskReading): void {
    this.current.update((inventory) =>
      inventory === null
        ? inventory
        : {
            ...inventory,
            groups: inventory.groups.map((group) => ({
              ...group,
              workflows: group.workflows.map((workflow) =>
                workflow.path === workflowPath
                  ? {
                      ...workflow,
                      risk: marking.level,
                      riskDecidedAt: marking.decidedAt,
                      executable: workflow.state === 'active' && marking.level !== 'unclassified',
                    }
                  : workflow,
              ),
            })),
          },
    );
  }

  private readonly riskSaving = signal<string | null>(null);
  private readonly riskError = signal<string | null>(null);

  readonly savingRiskFor = this.riskSaving.asReadonly();
  readonly riskFailure = this.riskError.asReadonly();

  /**
   * Reads the refs a run could target.
   *
   * A failed read leaves the registered branch as the only option rather than an empty list: a run still has a
   * sensible default, and the panel says the rest could not be read.
   */
  readBranches(projectId: string, defaultBranch: string): void {
    this.branchState.set('loading');
    this.branchList.set({ defaultBranch, branches: [defaultBranch], hasMore: false });

    this.dataSource
      .loadBranches(projectId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (branches) => {
          this.branchList.set(branches);
          this.branchState.set('loaded');
        },
        error: () => this.branchState.set('unavailable'),
      });
  }

  private readonly branchList = signal<WorkflowBranches | null>(null);
  private readonly branchState = signal<'idle' | 'loading' | 'loaded' | 'unavailable'>('idle');

  readonly branches = computed(() => this.branchList()?.branches ?? []);
  readonly branchesState = this.branchState.asReadonly();
  readonly branchesBounded = computed(() => this.branchList()?.hasMore ?? false);

  /**
   * Asks for a run, then looks for the run that request produced.
   *
   * The provider accepts a dispatch without reporting a run, so there is nothing to follow until one appears. The
   * screen says `Requested` in the meantime rather than claiming a run is going, and the refresh that already
   * follows running workflows is what finds it.
   */
  dispatch(
    projectId: string,
    workflow: Workflow,
    request: {
      readonly reference: string;
      readonly inputs: Readonly<Record<string, string>>;
      readonly confirmation: string | null;
    },
  ): void {
    this.dispatchState.set('requesting');
    this.dispatchError.set(null);

    this.dataSource
      .dispatch(projectId, workflow.id, request)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (accepted) => {
          this.dispatchState.set('accepted');
          this.requestedFor.set({ workflowId: workflow.id, at: accepted.requestedAt });
          // Ask straight away: the run usually appears within a second or two of being accepted.
          this.adoptRequestedRun(projectId, workflow.id);
        },
        error: (error: unknown) => {
          this.dispatchState.set('failed');
          this.dispatchError.set(dispatchMessage(error));
        },
      });
  }

  private readonly dispatchState = signal<'idle' | 'requesting' | 'accepted' | 'failed'>('idle');
  private readonly dispatchError = signal<string | null>(null);
  private readonly requestedFor = signal<{ workflowId: string; at: string } | null>(null);

  readonly dispatchStatus = this.dispatchState.asReadonly();
  readonly dispatchFailure = this.dispatchError.asReadonly();

  /** The workflow a run was requested for and not yet seen, so the screen can say so on that row. */
  readonly awaitingRunFor = computed(() => this.requestedFor()?.workflowId ?? null);

  clearDispatch(): void {
    this.dispatchState.set('idle');
    this.dispatchError.set(null);
    this.requestedFor.set(null);
  }

  /**
   * Looks for the run a dispatch produced and stops waiting once it is found.
   *
   * A run counts as the one requested when it started at or after the request was accepted. That is the only link
   * available: the provider never said which run it created, so anything stronger would be invented.
   */
  private adoptRequestedRun(projectId: string, workflowId: string): void {
    const requested = untracked(this.requestedFor);
    if (requested === null) {
      return;
    }

    this.dataSource
      .loadRuns(projectId, workflowId, 1)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (history) => {
          const latest = history.runs[0];
          if (latest === undefined) {
            return;
          }

          this.applyLatestRun(workflowId, history);

          const startedAt = latest.startedAt;
          if (startedAt !== null && Date.parse(startedAt) + 1000 >= Date.parse(requested.at)) {
            this.requestedFor.set(null);
            this.dispatchState.set('idle');
          }
        },
        error: () => undefined,
      });
  }

  /**
   * Reads the inventory.
   *
   * <paramref name="onDone"/> runs whether the read succeeded or not, so a caller waiting on it - a marking
   * holding its saving state until the screen reflects the change - is never left waiting on a failure.
   */
  read(onDone?: () => void): void {
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
          onDone?.();
        },
        error: () => {
          if (untracked(this.current) === null) {
            this.state.set('unavailable');
          }

          onDone?.();
        },
      });
  }

  /** Whether any workflow on the page has a run that has not finished. */
  /** A requested run that has not appeared yet is also something to follow closely. */
  readonly hasRunningWorkflow = computed(
    () =>
      this.requestedFor() !== null ||
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
    // A run was requested and has not appeared yet, so the thing to re-read is that workflow.
    const awaiting = this.requestedFor();
    if (awaiting !== null) {
      const projectId = this.projectOfWorkflow(awaiting.workflowId);
      if (projectId !== null) {
        this.adoptRequestedRun(projectId, awaiting.workflowId);
      }
    }

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

  private projectOfWorkflow(workflowId: string): string | null {
    for (const group of this.groups()) {
      if (group.workflows.some((workflow) => workflow.id === workflowId)) {
        return group.projectId;
      }
    }

    return null;
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

/**
 * What to tell an operator when a run was refused.
 *
 * The API's own reason is used where it gave one, because it names what to check - a token without write access,
 * a ref that does not exist, a marking that was withdrawn - and a generic message would send them looking.
 */
function dispatchMessage(error: unknown): string {
  const detail = (error as { error?: { detail?: string; title?: string } } | null)?.error;
  return detail?.detail ?? detail?.title ?? 'That run could not be started.';
}
