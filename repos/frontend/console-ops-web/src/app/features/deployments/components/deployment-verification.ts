import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { StatusLevel } from '../../../core/contracts/dashboard-overview';
import { DeploymentListItem } from '../../../core/contracts/deployment-registry';
import { deploymentVerdict } from '../../../core/ui/deployment-verdict';

interface VerificationStat {
  readonly label: string;
  readonly value: string;
  /** Indicator beside the figure, or `null` for a plain number. */
  readonly level: StatusLevel | null;
  /** `true` when the figure could not be established from the available records. */
  readonly unavailable?: boolean;
  /** `true` for figures that take a full row rather than a third. */
  readonly wide?: boolean;
}

const WEEK_MS = 7 * 24 * 60 * 60 * 1000;

/**
 * Verification summary.
 *
 * Every figure is counted from the deployment records in view. Nothing is estimated: when a figure has
 * no basis - no durations reported, no records in the window - it reads as unavailable rather than zero.
 */
@Component({
  selector: 'co-deployment-verification',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './deployment-verification.html',
  styleUrl: './deployment-verification.scss',
})
export class DeploymentVerification {
  readonly deployments = input.required<readonly DeploymentListItem[]>();
  readonly observedAt = input<string | null>(null);

  private readonly currentCount = computed(
    () => this.deployments().filter((deployment) => deployment.isCurrent).length,
  );

  private readonly failedToday = computed(() => {
    const today = this.observedAt()?.slice(0, 10) ?? null;
    if (today === null) {
      return null;
    }

    return this.deployments().filter(
      (deployment) =>
        deployment.deployedAt.slice(0, 10) === today &&
        deploymentVerdict(deployment).level === 'down',
    ).length;
  });

  private readonly verificationPassed = computed(() => {
    const deployments = this.deployments();
    if (deployments.length === 0) {
      return null;
    }

    const passed = deployments.filter(
      (deployment) => deploymentVerdict(deployment).level === 'healthy',
    ).length;
    return Math.round((passed / deployments.length) * 1000) / 10;
  });

  private readonly averageDuration = computed(() => {
    const reported = this.deployments()
      .map((deployment) => deployment.durationSeconds)
      .filter((seconds): seconds is number => seconds !== null);

    if (reported.length === 0) {
      return null;
    }

    const mean = reported.reduce((total, seconds) => total + seconds, 0) / reported.length;
    const whole = Math.round(mean);
    return whole < 60
      ? `${whole}s`
      : `${Math.floor(whole / 60)}m ${String(whole % 60).padStart(2, '0')}s`;
  });

  private readonly lastSevenDays = computed(() => {
    const observedAt = this.observedAt();
    if (observedAt === null) {
      return null;
    }

    const cutoff = Date.parse(observedAt) - WEEK_MS;
    return this.deployments().filter((deployment) => Date.parse(deployment.deployedAt) >= cutoff)
      .length;
  });

  protected readonly stats = computed<readonly VerificationStat[]>(() => {
    const failedToday = this.failedToday();
    const passed = this.verificationPassed();
    const average = this.averageDuration();
    const lastSevenDays = this.lastSevenDays();

    return [
      { label: 'Current deployments', value: `${this.currentCount()}`, level: 'running' },
      {
        label: 'Failed today',
        value: failedToday === null ? 'Unknown' : `${failedToday}`,
        level: failedToday === null ? null : failedToday > 0 ? 'down' : 'healthy',
        unavailable: failedToday === null,
      },
      {
        label: 'Verification passed',
        value: passed === null ? 'No records' : `${passed}%`,
        level: passed === null ? null : passed === 100 ? 'healthy' : 'warning',
        unavailable: passed === null,
      },
      {
        label: 'Average deployment time',
        value: average ?? 'Not reported',
        level: null,
        unavailable: average === null,
        wide: true,
      },
      {
        label: 'Deployments (7d)',
        value: lastSevenDays === null ? 'Unknown' : `${lastSevenDays}`,
        level: null,
        unavailable: lastSevenDays === null,
        wide: true,
      },
    ];
  });
}
