import { StatusCell } from '../contracts/dashboard-overview';
import { WorkflowRunConclusion, WorkflowRunStatus } from '../contracts/workflows';

/**
 * How a run's state reads on screen.
 *
 * One place, because the inventory row, the selected workflow and the job list all state the same thing and
 * three copies of this mapping would eventually disagree. Status comes first: a run that is still going has no
 * conclusion yet, and reporting `Passed` or `Failed` for it would invent an outcome.
 */
const CONCLUSION_CELLS: Readonly<Record<WorkflowRunConclusion, StatusCell>> = {
  passed: { level: 'healthy', label: 'Passed', detail: null },
  failed: { level: 'down', label: 'Failed', detail: null },
  cancelled: { level: 'unknown', label: 'Cancelled', detail: null },
  skipped: { level: 'notApplicable', label: 'Skipped', detail: null },
  timedOut: { level: 'down', label: 'Timed out', detail: null },
  actionRequired: { level: 'warning', label: 'Action required', detail: null },
  neutral: { level: 'notApplicable', label: 'Neutral', detail: null },
};

const ACTIVE_CELLS: Readonly<Record<WorkflowRunStatus, StatusCell | null>> = {
  queued: { level: 'unknown', label: 'Queued', detail: null },
  inProgress: { level: 'running', label: 'In progress', detail: null },
  waiting: { level: 'unknown', label: 'Waiting', detail: null },
  // A status Console Ops does not recognise is named as unrecognised, not rounded to a familiar one.
  unknown: { level: 'unknown', label: 'Unknown', detail: null },
  // A completed run is described by its conclusion, not by the fact that it stopped.
  completed: null,
};

/**
 * The cell for a run, or `null` when there is nothing to describe.
 *
 * A completed run with no conclusion returns `null` rather than a guess: the provider records both, and one
 * without the other means the fact was not collected.
 */
export function workflowRunCell(
  status: WorkflowRunStatus,
  conclusion: WorkflowRunConclusion | null,
): StatusCell | null {
  const active = ACTIVE_CELLS[status];
  if (active !== null) {
    return active;
  }

  return conclusion === null ? null : CONCLUSION_CELLS[conclusion];
}

/** Whether a run ended in a way an operator has to look at. */
export function isFailedRun(conclusion: WorkflowRunConclusion | null): boolean {
  return conclusion === 'failed' || conclusion === 'timedOut' || conclusion === 'actionRequired';
}

/** How a trigger reads, keeping the provider's own event recognisable. */
const TRIGGER_LABELS: Readonly<Record<string, string>> = {
  push: 'push',
  pullRequest: 'pull_request',
  schedule: 'schedule',
  manual: 'manual',
  workflowCall: 'workflow_call',
};

export function triggerLabel(trigger: string): string {
  return TRIGGER_LABELS[trigger] ?? trigger;
}
