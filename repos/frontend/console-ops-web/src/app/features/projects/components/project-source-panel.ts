import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { ProjectSurface, StatusCell } from '../../../core/contracts/dashboard-overview';
import { ProjectRepositoryRef } from '../../../core/contracts/project-registry';
import { Icon } from '../../../core/ui/icon';
import { Status } from '../../../core/ui/status';

/** Configured repository, plus the source and CI facts the last refresh observed. */
@Component({
  selector: 'co-project-source-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, Icon, Status],
  templateUrl: './project-source-panel.html',
  styleUrl: './project-source-panel.scss',
})
export class ProjectSourcePanel {
  readonly repository = input.required<ProjectRepositoryRef>();
  /** `null` until a refresh has read GitHub for this project. */
  readonly observed = input<ProjectSurface | null>(null);

  protected readonly repositoryUrl = computed(() => {
    const repository = this.repository();
    return `https://github.com/${repository.owner}/${repository.name}`;
  });

  /** Workflow state as a status cell; `notConfigured` when no workflow file is registered. */
  protected readonly workflow = computed<StatusCell | null>(() => {
    const surface = this.observed();
    if (surface === null) {
      return null;
    }

    switch (surface.workflow.state) {
      case 'passed':
        return { level: 'healthy', label: 'Passed', detail: surface.workflow.workflowName };
      case 'failed':
        return { level: 'down', label: 'Failed', detail: surface.workflow.workflowName };
      case 'inProgress':
        return { level: 'running', label: 'In progress', detail: surface.workflow.workflowName };
      case 'queued':
        return { level: 'running', label: 'Queued', detail: surface.workflow.workflowName };
      case 'cancelled':
        return { level: 'warning', label: 'Cancelled', detail: surface.workflow.workflowName };
      case 'notConfigured':
        return { level: 'notApplicable', label: 'Not configured', detail: null };
      default:
        return { level: 'unknown', label: 'Unknown', detail: null };
    }
  });
}
