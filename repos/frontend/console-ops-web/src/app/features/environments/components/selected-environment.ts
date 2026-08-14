import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';

import { EnvironmentListItem, StatusCell } from '../../../core/contracts/environment-registry';
import { EnvironmentTag } from '../../../core/ui/environment-tag';
import { Icon } from '../../../core/ui/icon';
import { ProjectMark, ProjectMarkTone } from '../../../core/ui/project-mark';
import { toneForProject } from '../../../core/ui/project-tone';
import { RelativeTimePipe } from '../../../core/ui/relative-time.pipe';
import { Status } from '../../../core/ui/status';

/** Full configuration and current state of the environment selected in the registry. */
@Component({
  selector: 'co-selected-environment',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [EnvironmentTag, Icon, ProjectMark, RelativeTimePipe, Status],
  templateUrl: './selected-environment.html',
  styleUrl: './selected-environment.scss',
})
export class SelectedEnvironment {
  readonly environment = input.required<EnvironmentListItem | null>();
  readonly observedAt = input<string | null>(null);
  readonly versionSync = input.required<StatusCell | null>();

  protected readonly copied = signal(false);

  protected readonly tone = computed<ProjectMarkTone>(() =>
    toneForProject(this.environment()?.projectId ?? ''),
  );

  /** Presence only: how many expected keys were found, never a value. */
  protected readonly configurationLabel = computed(() => {
    const configuration = this.environment()?.configuration ?? null;
    if (configuration === null) {
      return null;
    }
    return `${configuration.configuredKeys} / ${configuration.expectedKeys} configured`;
  });

  protected readonly configurationLevel = computed(() => {
    const configuration = this.environment()?.configuration ?? null;
    if (configuration === null) {
      return 'unknown' as const;
    }
    return configuration.configuredKeys === configuration.expectedKeys
      ? ('healthy' as const)
      : ('warning' as const);
  });

  protected async copyRevision(): Promise<void> {
    const revision = this.environment()?.currentRevision;
    if (revision === null || revision === undefined || !navigator.clipboard) {
      return;
    }

    await navigator.clipboard.writeText(revision);
    this.copied.set(true);
  }
}
