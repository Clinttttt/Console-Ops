import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';

import { StatusCell } from '../../core/contracts/dashboard-overview';
import {
  Workflow,
  WorkflowRun,
  WorkflowRunJob,
  WorkflowRunStep,
} from '../../core/contracts/workflows';
import {
  ACTIVE_PROVIDER_REFRESH_INTERVAL_MS,
  IDLE_PROVIDER_REFRESH_INTERVAL_MS,
  providerRefresh,
} from '../../core/state/auto-refresh';
import { WorkflowRunHistoryStore } from '../../core/state/workflow-run-history.store';
import { WorkflowsStore } from '../../core/state/workflows.store';
import { DurationPipe } from '../../core/ui/duration.pipe';
import { Icon, IconName } from '../../core/ui/icon';
import { RelativeTimePipe } from '../../core/ui/relative-time.pipe';
import { Status } from '../../core/ui/status';
import { triggerLabel, workflowRunCell } from '../../core/ui/workflow-run-state';

/**
 * One workflow's recent runs, and the jobs of whichever run is open.
 *
 * A screen of its own rather than more of the inventory: the inventory answers what automation exists and how it
 * last ran, and this answers what it has been doing lately. Opening a run reads its jobs, so "which step is this
 * stuck on" is answerable without leaving Console Ops.
 */
@Component({
  selector: 'co-workflow-runs-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DurationPipe, Icon, RelativeTimePipe, RouterLink, Status],
  templateUrl: './workflow-runs-page.html',
  styleUrl: './workflow-runs-page.scss',
})
export class WorkflowRunsPage implements OnInit {
  /** Bound from the route, so the screen can be opened directly rather than only from the inventory. */
  readonly projectId = input.required<string>();
  readonly workflowId = input.required<string>();

  private readonly history = inject(WorkflowRunHistoryStore);
  private readonly inventory = inject(WorkflowsStore);

  protected readonly loadState = this.history.loadState;
  protected readonly runs = this.history.runs;
  protected readonly hasMore = this.history.hasMore;
  protected readonly openRunId = this.history.openRunId;
  protected readonly jobsState = this.history.jobsState;
  protected readonly openRunJobs = this.history.openRunJobs;

  /**
   * The workflow this history belongs to, when the inventory has already been read.
   *
   * `null` on a direct visit: the runs endpoint answers with runs, not with the workflow's name, and inventing a
   * heading from an id would put a guess in the title. The screen then names what it does know.
   */
  protected readonly workflow = computed<{ workflow: Workflow; projectName: string } | null>(() => {
    for (const group of this.inventory.groups()) {
      if (group.projectId !== this.projectId()) {
        continue;
      }

      const match = group.workflows.find((candidate) => candidate.id === this.workflowId());
      if (match !== undefined) {
        return { workflow: match, projectName: group.projectName };
      }
    }

    return null;
  });

  /** When the runs were read, so "8 min ago" is relative to the read rather than to the browser clock. */
  protected readonly readAt = computed(() => this.inventory.readAt() ?? new Date().toISOString());

  constructor() {
    // The inventory may not have been read yet on a direct visit; it is what names the workflow.
    if (this.inventory.loadState() !== 'loaded') {
      this.inventory.read();
    }

    // A run in progress advances on its own here: the steps an operator is watching are the whole point of this
    // screen, so it follows them rather than waiting to be reloaded. Registered here because the refresh needs
    // an injection context; the route inputs it reads are set before the first tick.
    providerRefresh(
      () => this.history.refresh(this.projectId(), this.workflowId()),
      () =>
        this.history.hasRunningRun()
          ? ACTIVE_PROVIDER_REFRESH_INTERVAL_MS
          : IDLE_PROVIDER_REFRESH_INTERVAL_MS,
    );
  }

  ngOnInit(): void {
    this.history.read(this.projectId(), this.workflowId());
  }

  protected cellFor(run: WorkflowRun): StatusCell | null {
    return workflowRunCell(run.status, run.conclusion);
  }

  /** Which job's steps are open. One at a time: a run with ten jobs is a wall of steps otherwise. */
  private readonly openJobName = signal<string | null>(null);
  protected readonly openJob = this.openJobName.asReadonly();

  /** Shared by jobs and steps, which report the same states and so read the same. */
  protected jobCell(entry: WorkflowRunJob | WorkflowRunStep): StatusCell | null {
    return workflowRunCell(entry.status, entry.conclusion);
  }

  protected jobIcon(entry: WorkflowRunJob | WorkflowRunStep): IconName {
    if (entry.status !== 'completed') {
      return entry.status === 'inProgress' ? 'refresh' : 'pause';
    }

    return entry.conclusion === 'passed' ? 'checkCircle' : 'close';
  }

  protected toggleJob(job: WorkflowRunJob): void {
    if (job.steps.length === 0) {
      return;
    }

    this.openJobName.update((current) => (current === job.name ? null : job.name));
  }

  protected trigger(value: string): string {
    return triggerLabel(value);
  }

  protected toggle(run: WorkflowRun): void {
    this.history.toggleRun(this.projectId(), run.id);
  }
}
