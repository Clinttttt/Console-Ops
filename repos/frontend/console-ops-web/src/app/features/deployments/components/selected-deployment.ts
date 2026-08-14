import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';

import { StatusCell } from '../../../core/contracts/dashboard-overview';
import {
  DeploymentListItem,
  HealthCheckOutcome,
  VersionSyncState,
} from '../../../core/contracts/deployment-registry';
import { deploymentVerdict } from '../../../core/ui/deployment-verdict';
import { DurationPipe } from '../../../core/ui/duration.pipe';
import { EnvironmentTag } from '../../../core/ui/environment-tag';
import { Icon } from '../../../core/ui/icon';
import { ProjectMark, ProjectMarkTone } from '../../../core/ui/project-mark';
import { toneForProject } from '../../../core/ui/project-tone';
import { RelativeTimePipe } from '../../../core/ui/relative-time.pipe';
import { Status } from '../../../core/ui/status';

/** Full record of the deployment selected in the registry. */
@Component({
  selector: 'co-selected-deployment',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, DurationPipe, EnvironmentTag, Icon, ProjectMark, RelativeTimePipe, Status],
  templateUrl: './selected-deployment.html',
  styleUrl: './selected-deployment.scss',
})
export class SelectedDeployment {
  readonly deployment = input.required<DeploymentListItem | null>();
  readonly observedAt = input<string | null>(null);

  protected readonly copiedField = signal<'commit' | 'revision' | null>(null);

  protected readonly tone = computed<ProjectMarkTone>(() =>
    toneForProject(this.deployment()?.projectId ?? ''),
  );

  protected readonly verdict = computed<StatusCell | null>(() => {
    const deployment = this.deployment();
    return deployment === null ? null : deploymentVerdict(deployment);
  });

  protected readonly result = computed<StatusCell | null>(() => {
    const deployment = this.deployment();
    if (deployment === null) {
      return null;
    }

    switch (deployment.result) {
      case 'succeeded':
        return { level: 'healthy', label: 'Succeeded', detail: null };
      case 'failed':
        return { level: 'down', label: 'Failed', detail: null };
      case 'cancelled':
        return { level: 'unknown', label: 'Cancelled', detail: null };
      case 'inProgress':
        return { level: 'running', label: 'In progress', detail: null };
      default:
        return { level: 'unknown', label: 'Unknown', detail: null };
    }
  });

  protected readonly healthCheck = computed<StatusCell | null>(() => {
    const deployment = this.deployment();
    return deployment === null ? null : healthCell(deployment.healthCheck);
  });

  protected readonly versionCheck = computed<StatusCell | null>(() => {
    const deployment = this.deployment();
    return deployment === null ? null : versionCell(deployment.versionCheck);
  });

  protected async copy(field: 'commit' | 'revision', value: string | null): Promise<void> {
    if (value === null || !navigator.clipboard) {
      return;
    }

    await navigator.clipboard.writeText(value);
    this.copiedField.set(field);
  }
}

function healthCell(outcome: HealthCheckOutcome): StatusCell {
  switch (outcome) {
    case 'passed':
      return { level: 'healthy', label: 'Passed', detail: null };
    case 'failed':
      return { level: 'down', label: 'Failed', detail: null };
    case 'notConfigured':
      return { level: 'notApplicable', label: 'Not configured', detail: null };
    default:
      return { level: 'unknown', label: 'Unknown', detail: null };
  }
}

function versionCell(state: VersionSyncState): StatusCell {
  switch (state) {
    case 'inSync':
      return { level: 'healthy', label: 'In Sync', detail: null };
    case 'behind':
      return { level: 'warning', label: 'Behind', detail: null };
    case 'notConfigured':
      return { level: 'notApplicable', label: 'Not configured', detail: null };
    default:
      return { level: 'unknown', label: 'Unknown', detail: null };
  }
}
