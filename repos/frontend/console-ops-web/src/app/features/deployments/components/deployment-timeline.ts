import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';

import { StatusCell } from '../../../core/contracts/dashboard-overview';
import {
  DeploymentEnvironmentObservation,
  DeploymentListItem,
} from '../../../core/contracts/deployment-registry';
import { deploymentVerdict } from '../../../core/ui/deployment-verdict';
import { DurationPipe } from '../../../core/ui/duration.pipe';
import { EnvironmentTag } from '../../../core/ui/environment-tag';
import { Icon } from '../../../core/ui/icon';
import { ProjectMark, ProjectMarkTone } from '../../../core/ui/project-mark';
import { toneForProject } from '../../../core/ui/project-tone';
import { RelativeTimePipe } from '../../../core/ui/relative-time.pipe';
import { Status } from '../../../core/ui/status';

interface DeploymentDay {
  /** `YYYY-MM-DD` in UTC, used as the group key. */
  readonly key: string;
  /**
   * A full instant from the group. `DatePipe` reads a date-only string as local time, which can shift
   * the rendered day, so the label is formatted from a real instant in UTC instead.
   */
  readonly instant: string;
  readonly isToday: boolean;
  readonly deployments: readonly DeploymentListItem[];
}

/**
 * Release history as a timeline of cards, newest first.
 *
 * A wide table forced every fact into its own column and became unreadable. Each release is now one
 * card: the scannable line carries project, the environments it was observed running in, the verdict,
 * timing, and actions, while source detail sits on a quieter second line.
 */
@Component({
  selector: 'co-deployment-timeline',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, DurationPipe, EnvironmentTag, Icon, ProjectMark, RelativeTimePipe, Status],
  templateUrl: './deployment-timeline.html',
  styleUrl: './deployment-timeline.scss',
})
export class DeploymentTimeline {
  readonly deployments = input.required<readonly DeploymentListItem[]>();
  readonly totalCount = input.required<number>();
  readonly selectedId = input<string | null>(null);
  readonly observedAt = input<string | null>(null);

  readonly selectDeployment = output<string>();
  readonly clearFilters = output<void>();

  protected readonly copiedId = signal<string | null>(null);

  protected readonly days = computed<readonly DeploymentDay[]>(() => {
    const observedDay = this.observedAt()?.slice(0, 10) ?? null;
    const groups = new Map<string, DeploymentListItem[]>();

    for (const deployment of this.deployments()) {
      const key = deployment.deployedAt.slice(0, 10);
      const bucket = groups.get(key);
      if (bucket === undefined) {
        groups.set(key, [deployment]);
      } else {
        bucket.push(deployment);
      }
    }

    return [...groups.entries()]
      .sort(([left], [right]) => right.localeCompare(left))
      .map(([key, deployments]) => ({
        key,
        instant: deployments[0].deployedAt,
        isToday: key === observedDay,
        deployments: [...deployments].sort((left, right) =>
          right.deployedAt.localeCompare(left.deployedAt),
        ),
      }));
  });

  protected verdict(deployment: DeploymentListItem): StatusCell {
    return deploymentVerdict(deployment);
  }

  protected toneFor(projectId: string): ProjectMarkTone {
    return toneForProject(projectId);
  }

  /** Environments sorted so the ones still serving the release are named first. */
  protected environmentsFor(
    deployment: DeploymentListItem,
  ): readonly DeploymentEnvironmentObservation[] {
    return [...deployment.environments].sort(
      (left, right) => Number(right.isCurrent) - Number(left.isCurrent),
    );
  }

  /** Workflow file plus run number, which is how a run is identified in GitHub. */
  protected runLabel(deployment: DeploymentListItem): string | null {
    const file = deployment.workflowFile ?? deployment.workflowName;
    if (file === null) {
      return null;
    }

    return deployment.runNumber === null ? file : `${file} #${deployment.runNumber}`;
  }

  protected async copyCommit(deployment: DeploymentListItem): Promise<void> {
    if (!navigator.clipboard) {
      return;
    }

    await navigator.clipboard.writeText(deployment.commitSha);
    this.copiedId.set(deployment.id);
  }
}
