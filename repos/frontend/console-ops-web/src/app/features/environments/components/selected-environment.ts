import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';

import { StatusCell } from '../../../core/contracts/dashboard-overview';
import { EnvironmentRow } from '../environments-page';
import { EnvironmentTag } from '../../../core/ui/environment-tag';
import { Icon } from '../../../core/ui/icon';
import { ProjectMark, ProjectMarkTone } from '../../../core/ui/project-mark';
import { toneForProject } from '../../../core/ui/project-tone';
import { Status } from '../../../core/ui/status';

/**
 * Configuration and current state of the environment selected in the list.
 *
 * Everything shown is either registered configuration or a stored observation. Editing happens through
 * the project, so this offers a link there rather than a control that does nothing.
 */
@Component({
  selector: 'co-selected-environment',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, EnvironmentTag, Icon, ProjectMark, RouterLink, Status],
  templateUrl: './selected-environment.html',
  styleUrl: './selected-environment.scss',
})
export class SelectedEnvironment {
  readonly environment = input.required<EnvironmentRow | null>();
  readonly versionSync = input.required<StatusCell | null>();

  protected readonly tone = computed<ProjectMarkTone>(() =>
    toneForProject(this.environment()?.projectId ?? ''),
  );
}
