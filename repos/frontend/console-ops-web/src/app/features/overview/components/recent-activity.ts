import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { ActivityEntry, ActivityKind } from '../../../core/contracts/dashboard-overview';
import { Icon, IconName } from '../../../core/ui/icon';

const ACTIVITY_ICONS: Readonly<Record<ActivityKind, IconName>> = {
  healthFailed: 'pulse',
  healthRecovered: 'pulse',
  versionDrift: 'refresh',
  versionSynchronized: 'refresh',
};

/**
 * How many events the rail shows before the rest are left to the activity screen.
 *
 * Chosen so the rail stays scannable beside the surfaces table rather than running past it: an operator reads
 * this to notice something, not to audit history.
 */
const VisibleEntries = 9;

/** Chronological stream of operational events across every environment. */
@Component({
  selector: 'co-recent-activity',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, Icon],
  templateUrl: './recent-activity.html',
  styleUrl: './recent-activity.scss',
})
export class RecentActivitySection {
  readonly entries = input.required<readonly ActivityEntry[]>();

  protected readonly visible = computed(() => this.entries().slice(0, VisibleEntries));

  /** How many recorded events are not shown, so the rail never implies it is the whole history. */
  protected readonly hidden = computed(() => Math.max(0, this.entries().length - VisibleEntries));

  protected iconFor(kind: ActivityKind): IconName {
    return ACTIVITY_ICONS[kind];
  }
}
