import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

import {
  EnvironmentKind,
  StatusCell,
  StatusLevel,
} from '../../../core/contracts/dashboard-overview';
import { EnvironmentRow } from '../environments-page';
import { EnvironmentTag } from '../../../core/ui/environment-tag';
import { Icon } from '../../../core/ui/icon';
import { ProjectMark, ProjectMarkTone } from '../../../core/ui/project-mark';
import { toneForProject } from '../../../core/ui/project-tone';
import { Status } from '../../../core/ui/status';

/** Group order and indicator colour for each environment kind. */
const KINDS: readonly { kind: EnvironmentKind; label: string; level: StatusLevel }[] = [
  { kind: 'production', label: 'Production', level: 'healthy' },
  { kind: 'staging', label: 'Staging', level: 'running' },
  { kind: 'development', label: 'Development', level: 'warning' },
  { kind: 'local', label: 'Local', level: 'unknown' },
];

interface EnvironmentGroup {
  readonly kind: EnvironmentKind;
  readonly label: string;
  readonly level: StatusLevel;
  readonly environments: readonly EnvironmentRow[];
}

/**
 * Environments grouped by kind, one row per environment.
 *
 * Only V1 facts appear: configuration from the project resource and observations from the stored
 * overview. An environment with no observation says so rather than borrowing another's state.
 */
@Component({
  selector: 'co-environment-groups',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [EnvironmentTag, Icon, ProjectMark, Status],
  templateUrl: './environment-groups.html',
  styleUrl: './environment-groups.scss',
})
export class EnvironmentGroups {
  readonly environments = input.required<readonly EnvironmentRow[]>();
  readonly totalCount = input.required<number>();
  readonly selectedId = input<string | null>(null);

  readonly selectEnvironment = output<string>();
  readonly clearFilters = output<void>();

  /** Only kinds that actually have environments appear, so no group is ever empty. */
  protected readonly groups = computed<readonly EnvironmentGroup[]>(() => {
    const rows = this.environments();

    return KINDS.map((entry) => ({
      ...entry,
      environments: rows.filter((row) => row.kind === entry.kind),
    })).filter((group) => group.environments.length > 0);
  });

  protected toneFor(projectId: string): ProjectMarkTone {
    return toneForProject(projectId);
  }

  /** Version sync is a deterministic decision, so its wording is fixed here, not in the payload. */
  protected versionSyncCell(row: EnvironmentRow): StatusCell | null {
    if (row.observed === null) {
      return null;
    }

    switch (row.observed.versionSync.state) {
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
}
