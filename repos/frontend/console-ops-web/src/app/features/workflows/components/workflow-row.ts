import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

import { StatusCell } from '../../../core/contracts/dashboard-overview';
import { Workflow } from '../../../core/contracts/workflows';
import { Icon, IconName } from '../../../core/ui/icon';
import { RelativeTimePipe } from '../../../core/ui/relative-time.pipe';
import { Status } from '../../../core/ui/status';
import { triggerLabel, workflowRunCell } from '../../../core/ui/workflow-run-state';

/**
 * One workflow in the inventory: what it is, how its last run ended, and what can be done with it.
 *
 * Its own component so selecting a workflow re-renders two rows rather than the whole inventory, and so the
 * page stylesheet stays within budget.
 */
@Component({
  selector: 'co-workflow-row',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon, RelativeTimePipe, Status],
  templateUrl: './workflow-row.html',
  styleUrl: './workflow-row.scss',
})
export class WorkflowRow {
  readonly workflow = input.required<Workflow>();
  readonly selected = input(false);
  /** When the inventory was read, so "8 min ago" is relative to the read rather than to the browser clock. */
  readonly readAt = input.required<string | null>();

  readonly choose = output<Workflow>();

  /**
   * One glyph for every workflow, and a deployment glyph only where a deployment was configured.
   *
   * A database icon for "Database backup" would be inferred from its name, which is exactly the kind of clever
   * guess that makes a screen look knowledgeable while being wrong.
   */
  protected readonly icon = computed<IconName>(() =>
    this.workflow().classification === 'deployment' ? 'rocket' : 'ciCd',
  );

  protected readonly runCell = computed<StatusCell | null>(() => {
    const run = this.workflow().latestRun;
    return run === null ? null : workflowRunCell(run.status, run.conclusion);
  });

  protected trigger(value: string): string {
    return triggerLabel(value);
  }

  protected select(): void {
    this.choose.emit(this.workflow());
  }
}
