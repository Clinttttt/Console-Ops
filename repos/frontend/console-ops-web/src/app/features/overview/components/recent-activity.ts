import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { ActivityEntry, ActivityKind } from '../../../core/contracts/dashboard-overview';
import { Icon, IconName } from '../../../core/ui/icon';

const ACTIVITY_ICONS: Readonly<Record<ActivityKind, IconName>> = {
  healthFailed: 'pulse',
  healthRecovered: 'pulse',
  versionDrift: 'refresh',
  versionSynchronized: 'refresh',
};

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

  protected iconFor(kind: ActivityKind): IconName {
    return ACTIVITY_ICONS[kind];
  }
}
