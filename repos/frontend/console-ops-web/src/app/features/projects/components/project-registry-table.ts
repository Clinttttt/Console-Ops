import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

import { ProjectListItem } from '../../../core/contracts/project-registry';
import { EnvironmentTag } from '../../../core/ui/environment-tag';
import { Icon } from '../../../core/ui/icon';
import { ProjectMark, ProjectMarkTone } from '../../../core/ui/project-mark';
import { toneForProject } from '../../../core/ui/project-tone';

/** The registry table renders only persisted V1 project configuration. */
@Component({
  selector: 'co-project-registry-table',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, EnvironmentTag, Icon, ProjectMark],
  templateUrl: './project-registry-table.html',
  styleUrl: './project-registry-table.scss',
})
export class ProjectRegistryTable {
  readonly projects = input.required<readonly ProjectListItem[]>();
  readonly clearFilters = output<void>();

  protected repository(project: ProjectListItem): string {
    return `${project.repository.owner}/${project.repository.name}`;
  }

  protected monitoringLabel(project: ProjectListItem): string {
    const hasHealth = project.environments.some(({ healthUrl }) => healthUrl !== null);
    const hasVersion = project.environments.some(({ versionUrl }) => versionUrl !== null);

    if (hasHealth && hasVersion) return 'Health + version';
    if (hasHealth) return 'Health';
    if (hasVersion) return 'Version';
    return 'Not configured';
  }

  protected toneFor(projectId: string): ProjectMarkTone {
    return toneForProject(projectId);
  }
}
