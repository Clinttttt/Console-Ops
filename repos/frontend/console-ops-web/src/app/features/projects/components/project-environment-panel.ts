import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { StatusCell } from '../../../core/contracts/dashboard-overview';
import { EnvironmentView } from '../project-detail-page';
import { EnvironmentTag } from '../../../core/ui/environment-tag';
import { Icon } from '../../../core/ui/icon';
import { Status } from '../../../core/ui/status';

/**
 * One environment: what is configured, and what has been observed about it.
 *
 * An environment with no observation reports that plainly. Nothing is inferred from another
 * environment, and a missing endpoint reads as `Not configured` rather than as a failure.
 */
@Component({
  selector: 'co-project-environment-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, EnvironmentTag, Icon, Status],
  templateUrl: './project-environment-panel.html',
  styleUrl: './project-environment-panel.scss',
})
export class ProjectEnvironmentPanel {
  readonly view = input.required<EnvironmentView>();

  protected readonly versionSync = computed<StatusCell | null>(() => {
    const observed = this.view().observed;
    if (observed === null) {
      return null;
    }

    const { state, commitsBehind } = observed.versionSync;
    switch (state) {
      case 'inSync':
        return { level: 'healthy', label: 'In Sync', detail: null };
      case 'behind':
        return {
          level: 'warning',
          label: 'Behind',
          detail: commitsBehind === null ? null : `${commitsBehind} commits`,
        };
      case 'notConfigured':
        return { level: 'notApplicable', label: 'Not configured', detail: null };
      default:
        return { level: 'unknown', label: 'Unknown', detail: null };
    }
  });
}
