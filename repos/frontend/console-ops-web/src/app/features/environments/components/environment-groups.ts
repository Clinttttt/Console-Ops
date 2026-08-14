import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

import { StatusCell, StatusLevel } from '../../../core/contracts/dashboard-overview';
import {
  EnvironmentKind,
  EnvironmentListItem,
  RuntimeProvider,
} from '../../../core/contracts/environment-registry';
import { EnvironmentTag } from '../../../core/ui/environment-tag';
import { Icon, IconName } from '../../../core/ui/icon';
import { ProjectMark, ProjectMarkTone } from '../../../core/ui/project-mark';
import { toneForProject } from '../../../core/ui/project-tone';
import { RelativeTimePipe } from '../../../core/ui/relative-time.pipe';
import { Status } from '../../../core/ui/status';

const RUNTIME_ICONS: Readonly<Record<RuntimeProvider, IconName>> = {
  azure: 'azure',
  docker: 'docker',
  other: 'cube',
};

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
  readonly environments: readonly EnvironmentListItem[];
}

/**
 * Environments grouped by kind, one card per environment.
 *
 * A wide table forced runtime, URL, health, version sync, and timing into competing columns. Each
 * environment is now a single readable line, and the group heading carries the count for that kind, so
 * the screen needs no separate distribution panel. Full configuration lives in the detail rail.
 */
@Component({
  selector: 'co-environment-groups',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [EnvironmentTag, Icon, ProjectMark, RelativeTimePipe, Status],
  templateUrl: './environment-groups.html',
  styleUrl: './environment-groups.scss',
})
export class EnvironmentGroups {
  readonly environments = input.required<readonly EnvironmentListItem[]>();
  readonly totalCount = input.required<number>();
  readonly selectedId = input<string | null>(null);
  readonly observedAt = input<string | null>(null);
  readonly showingArchived = input(false);

  readonly selectEnvironment = output<string>();
  readonly clearFilters = output<void>();
  readonly viewArchived = output<void>();

  /** Only kinds that actually have environments appear, so no group is ever empty. */
  protected readonly groups = computed<readonly EnvironmentGroup[]>(() => {
    const environments = this.environments();

    return KINDS.map((entry) => ({
      ...entry,
      environments: environments.filter((environment) => environment.kind === entry.kind),
    })).filter((group) => group.environments.length > 0);
  });

  protected toneFor(projectId: string): ProjectMarkTone {
    return toneForProject(projectId);
  }

  protected runtimeIcon(provider: RuntimeProvider | null): IconName {
    return provider === null ? 'cube' : RUNTIME_ICONS[provider];
  }

  /** Version sync is a deterministic decision, so its wording is fixed here, not in the payload. */
  protected versionSyncCell(environment: EnvironmentListItem): StatusCell {
    const { state, deployedCommitShortSha } = environment.versionSync;

    switch (state) {
      case 'inSync':
        return { level: 'healthy', label: 'In Sync', detail: deployedCommitShortSha };
      case 'behind':
        return { level: 'warning', label: 'Behind', detail: deployedCommitShortSha };
      case 'notConfigured':
        return { level: 'notApplicable', label: 'Not configured', detail: null };
      default:
        return { level: 'unknown', label: 'Unknown', detail: null };
    }
  }
}
