import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

import { ProjectKind, ProjectListItem } from '../../../core/contracts/project-registry';
import { EnvironmentTag } from '../../../core/ui/environment-tag';
import { Icon } from '../../../core/ui/icon';
import { ProjectMark, ProjectMarkTone } from '../../../core/ui/project-mark';
import { toneForProject } from '../../../core/ui/project-tone';
import { Status } from '../../../core/ui/status';

const KIND_LABELS: Readonly<Record<ProjectKind, string>> = {
  api: 'API',
  webApp: 'Web App',
  worker: 'Worker',
};

/** The registry table: every registered project and its current configuration and state. */
@Component({
  selector: 'co-project-registry-table',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, EnvironmentTag, Icon, ProjectMark, Status],
  templateUrl: './project-registry-table.html',
  styleUrl: './project-registry-table.scss',
})
export class ProjectRegistryTable {
  readonly projects = input.required<readonly ProjectListItem[]>();
  readonly showingArchived = input(false);

  readonly clearFilters = output<void>();
  readonly viewArchived = output<void>();

  protected kindLabel(kind: ProjectKind | null): string | null {
    return kind === null ? null : KIND_LABELS[kind];
  }

  protected toneFor(projectId: string): ProjectMarkTone {
    return toneForProject(projectId);
  }
}
