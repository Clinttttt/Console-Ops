import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';

import { StatusCell } from '../../../core/contracts/dashboard-overview';
import {
  DeploymentEnvironmentObservation,
  DeploymentListItem,
} from '../../../core/contracts/deployment-registry';
import { deploymentVerdict } from '../../../core/ui/deployment-verdict';
import { DurationPipe } from '../../../core/ui/duration.pipe';
import { EnvironmentTag } from '../../../core/ui/environment-tag';
import { healthStateCell, versionSyncCell } from '../../../core/ui/health-state';
import { Icon } from '../../../core/ui/icon';
import { ProjectMark, ProjectMarkTone } from '../../../core/ui/project-mark';
import { toneForProject } from '../../../core/ui/project-tone';
import { RelativeTimePipe } from '../../../core/ui/relative-time.pipe';
import { Status } from '../../../core/ui/status';

/** One environment's evidence for the selected release, resolved for display. */
interface EnvironmentEvidence {
  readonly id: string;
  readonly name: string;
  readonly kind: DeploymentEnvironmentObservation['environment']['kind'];
  readonly isCurrent: boolean;
  readonly firstObservedAt: string;
  readonly healthBefore: StatusCell;
  readonly healthBeforeObservedAt: string | null;
  readonly healthAfter: StatusCell;
  readonly healthAfterObservedAt: string | null;
  readonly versionCheck: StatusCell;
}

/**
 * Full record of the selected release.
 *
 * The rail answers two questions the timeline cannot: exactly what the run was, and what the runtime
 * reported either side of it. Health before and after are the observations bracketing the first sighting
 * of this commit, which is what makes a bad release visible.
 */
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

  protected readonly copied = signal(false);

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
      case 'passed':
        return { level: 'healthy', label: 'Passed', detail: null };
      case 'failed':
        return { level: 'down', label: 'Failed', detail: null };
      case 'cancelled':
        return { level: 'unknown', label: 'Cancelled', detail: null };
      case 'inProgress':
        return { level: 'running', label: 'In progress', detail: null };
      case 'queued':
        return { level: 'running', label: 'Queued', detail: null };
      default:
        return { level: 'unknown', label: 'Unknown', detail: null };
    }
  });

  /** Run identity as GitHub shows it, or `null` when no workflow is configured. */
  protected readonly runLabel = computed<string | null>(() => {
    const deployment = this.deployment();
    if (deployment === null) {
      return null;
    }

    const file = deployment.workflowFile ?? deployment.workflowName;
    if (file === null) {
      return null;
    }

    return deployment.runNumber === null ? file : `${file} #${deployment.runNumber}`;
  });

  protected readonly environments = computed<readonly EnvironmentEvidence[]>(() => {
    const deployment = this.deployment();
    if (deployment === null) {
      return [];
    }

    return [...deployment.environments]
      .sort((left, right) => Number(right.isCurrent) - Number(left.isCurrent))
      .map((observation) => ({
        id: observation.environment.id,
        name: observation.environment.name,
        kind: observation.environment.kind,
        isCurrent: observation.isCurrent,
        firstObservedAt: observation.firstObservedAt,
        healthBefore: healthStateCell(observation.healthBefore),
        healthBeforeObservedAt: observation.healthBeforeObservedAt,
        healthAfter: healthStateCell(observation.healthAfter),
        healthAfterObservedAt: observation.healthAfterObservedAt,
        versionCheck: versionSyncCell(observation.versionCheck),
      }));
  });

  protected async copyCommit(): Promise<void> {
    const deployment = this.deployment();
    if (deployment === null || !navigator.clipboard) {
      return;
    }

    await navigator.clipboard.writeText(deployment.commitSha);
    this.copied.set(true);
  }
}
