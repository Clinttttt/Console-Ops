import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { StatusCell } from '../../../core/contracts/dashboard-overview';
import { Workflow, WorkflowRunJob } from '../../../core/contracts/workflows';
import { DurationPipe } from '../../../core/ui/duration.pipe';
import { RouterLink } from '@angular/router';

import { Icon, IconName } from '../../../core/ui/icon';
import { Status } from '../../../core/ui/status';
import { triggerLabel, workflowRunCell } from '../../../core/ui/workflow-run-state';

/**
 * The selected workflow: what it is, and where its latest run has reached.
 *
 * Deliberately narrow. Project and environment facts have their own screens, and repeating them here would make
 * this a second project page that can disagree with the first.
 */
@Component({
  selector: 'co-workflow-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, DurationPipe, Icon, RouterLink, Status],
  templateUrl: './workflow-detail.html',
  styleUrl: './workflow-detail.scss',
})
export class WorkflowDetail {
  readonly workflow = input.required<Workflow>();
  readonly projectName = input.required<string>();
  /** Which project owns it, so the panel can link to this workflow's run history. */
  readonly projectId = input.required<string>();

  /** Read on demand for this workflow's latest run, so the panel states what it is waiting for. */
  readonly jobs = input<readonly WorkflowRunJob[]>([]);
  readonly jobsState = input<'idle' | 'loading' | 'unavailable' | 'loaded'>('idle');

  protected readonly icon = computed<IconName>(() =>
    this.workflow().classification === 'deployment' ? 'rocket' : 'ciCd',
  );

  protected readonly runCell = computed<StatusCell | null>(() => {
    const run = this.workflow().latestRun;
    return run === null ? null : workflowRunCell(run.status, run.conclusion);
  });

  /** How the workflow's type reads. Only a configured deployment earns a claim. */
  protected readonly classification = computed(() =>
    this.workflow().classification === 'deployment' ? 'Deployment' : 'Unclassified',
  );

  protected readonly manualRun = computed(() => {
    switch (this.workflow().manualRun) {
      case 'supported':
        return 'Supported';
      case 'unavailable':
        return 'Not available';
      default:
        return null;
    }
  });

  protected jobCell(job: WorkflowRunJob): StatusCell | null {
    return workflowRunCell(job.status, job.conclusion);
  }

  /** The icon beside a job, from its state alone. */
  protected jobIcon(job: WorkflowRunJob): IconName {
    if (job.status !== 'completed') {
      return job.status === 'inProgress' ? 'refresh' : 'pause';
    }

    return job.conclusion === 'passed' ? 'checkCircle' : 'close';
  }

  protected trigger(value: string): string {
    return triggerLabel(value);
  }
}
