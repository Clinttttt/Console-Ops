import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

import { EnvironmentHealth } from '../../../core/contracts/health';
import { healthCheckCell } from '../../../core/ui/health-check-state';
import { Icon } from '../../../core/ui/icon';
import { ProjectMark, ProjectMarkTone } from '../../../core/ui/project-mark';
import { toneForProject } from '../../../core/ui/project-tone';
import { Status } from '../../../core/ui/status';

/**
 * One monitored environment and what its last check found.
 *
 * Its own `OnPush` component so selecting a row re-renders two rows rather than the whole list, and so each
 * row's wording is computed once instead of on every change detection pass.
 *
 * The dependency checks are shown beside the verdict because they are the reason for it: an environment reading
 * `Degraded` while its application answers in 112ms is a dependency problem, and the row has to make that
 * visible without being opened.
 */
@Component({
  selector: 'co-health-row',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon, ProjectMark, Status],
  templateUrl: './health-row.html',
  styleUrl: './health-row.scss',
})
export class HealthRow {
  readonly environment = input.required<EnvironmentHealth>();
  readonly isSelected = input(false);
  readonly observedAt = input<string | null>(null);

  readonly selectEnvironment = output<string>();

  protected readonly verdict = computed(() => healthCheckCell(this.environment().state));

  /** Presentation stays in the UI: the contract carries the project, and the tone is derived from its id. */
  protected readonly tone = computed<ProjectMarkTone>(() =>
    toneForProject(this.environment().projectId),
  );

  protected readonly checks = computed(() =>
    this.environment().checks.map((check) => ({
      ...check,
      cell: healthCheckCell(check.state),
    })),
  );

  /** When the last check ran, worded relative to the response so a stale screen cannot look current. */
  protected readonly checkedLabel = computed(() => {
    const checkedAt = this.environment().checkedAt;
    if (checkedAt === null) {
      return null;
    }

    const observedAt = this.observedAt();
    if (observedAt === null) {
      return null;
    }

    const seconds = Math.max(
      0,
      Math.round((Date.parse(observedAt) - Date.parse(checkedAt)) / 1000),
    );
    if (seconds < 60) {
      return `Checked ${seconds} sec ago`;
    }

    const minutes = Math.round(seconds / 60);
    return minutes < 60
      ? `Checked ${minutes} min ago`
      : `Checked ${Math.round(minutes / 60)} h ago`;
  });
}
