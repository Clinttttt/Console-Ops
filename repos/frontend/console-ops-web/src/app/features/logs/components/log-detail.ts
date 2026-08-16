import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';

import { LogEvent, LogStreamScope } from '../../../core/contracts/log-stream';
import { EnvironmentTag } from '../../../core/ui/environment-tag';
import { Icon } from '../../../core/ui/icon';

type CopyField = 'trace' | 'request';

/**
 * Everything about one event that the stream deliberately withheld.
 *
 * Opened by selection and dismissable: the stream is the workspace, and a panel describing a line nobody
 * chose would be describing something arbitrary. A stack trace stays collapsed, because the moment it is
 * expanded by default this panel becomes a wall.
 */
@Component({
  selector: 'co-log-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, EnvironmentTag, Icon],
  templateUrl: './log-detail.html',
  styleUrl: './log-detail.scss',
})
export class LogDetail {
  readonly event = input.required<LogEvent | null>();
  readonly scope = input.required<LogStreamScope | null>();

  readonly dismiss = output<void>();

  protected readonly copied = signal<CopyField | null>(null);
  protected readonly stackTraceOpen = signal(false);

  protected readonly levelLabel = computed(() => {
    switch (this.event()?.level) {
      case 'error':
        return 'Error';
      case 'warning':
        return 'Warning';
      case 'info':
        return 'Info';
      default:
        return null;
    }
  });

  /** Wording for where the event came from. The contract sends the machine value. */
  protected readonly sourceKindLabel = computed(() => {
    switch (this.event()?.sourceKind) {
      case 'runtime':
        return 'Runtime';
      case 'platform':
        return 'Platform';
      case 'application':
        return 'Application';
      default:
        return null;
    }
  });

  protected readonly durationLabel = computed(() => {
    const duration = this.event()?.outcome?.durationMs ?? null;
    if (duration === null) {
      return null;
    }

    return duration < 1000
      ? `${Math.round(duration)} ms`
      : `${(duration / 1000).toFixed(duration < 10_000 ? 2 : 1)} s`;
  });

  protected async copy(field: CopyField, value: string | null): Promise<void> {
    if (value === null || !navigator.clipboard) {
      return;
    }

    await navigator.clipboard.writeText(value);
    this.copied.set(field);
  }

  protected toggleStackTrace(): void {
    this.stackTraceOpen.update((open) => !open);
  }
}
