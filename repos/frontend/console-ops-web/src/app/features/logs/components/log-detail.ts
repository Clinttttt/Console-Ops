import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';

import { LogEvent, LogStreamScope } from '../../../core/contracts/log-stream';
import { EnvironmentTag } from '../../../core/ui/environment-tag';
import { Icon } from '../../../core/ui/icon';

/**
 * Everything about one event that the stream deliberately withheld.
 *
 * Opened by selection and dismissable: the stream is the workspace, and a panel describing a line nobody
 * chose would be describing something arbitrary. A stack trace stays collapsed, because the moment it is
 * expanded by default this panel becomes a wall.
 *
 * What it shows is bounded by what a console line can carry. Severity and category are parsed from the text
 * and labelled as derived; trace ids and structured properties are absent rather than empty, because the
 * provider has none to give.
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

  protected readonly copied = signal(false);
  protected readonly stackTraceOpen = signal(false);

  protected readonly levelLabel = computed(() => {
    switch (this.event()?.level) {
      case 'trace':
        return 'Trace';
      case 'debug':
        return 'Debug';
      case 'information':
        return 'Info';
      case 'warning':
        return 'Warning';
      case 'error':
        return 'Error';
      case 'critical':
        return 'Critical';
      case 'unknown':
        return 'Log';
      default:
        return null;
    }
  });

  /** Groups the seven contract levels onto the three tones the design system uses. */
  protected readonly levelTone = computed(() => {
    switch (this.event()?.level) {
      case 'error':
      case 'critical':
        return 'error';
      case 'warning':
        return 'warning';
      case 'information':
        return 'info';
      default:
        return 'unknown';
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

  /**
   * How long the provider took to ingest the line. Worth showing because the stream orders by the emitter's
   * clock: a large gap explains why something appears later than it happened.
   */
  protected readonly ingestionDelay = computed(() => {
    const event = this.event();
    if (event === null || event.receivedAt === null) {
      return null;
    }

    const delay = Date.parse(event.receivedAt) - Date.parse(event.occurredAt);
    if (Number.isNaN(delay) || delay < 0) {
      return null;
    }

    return delay < 1000 ? `${delay} ms` : `${(delay / 1000).toFixed(1)} s`;
  });

  protected async copyMessage(): Promise<void> {
    const event = this.event();
    if (event === null || !navigator.clipboard) {
      return;
    }

    const text =
      event.stackTrace === null ? event.message : `${event.message}\n${event.stackTrace}`;
    await navigator.clipboard.writeText(text);
    this.copied.set(true);
  }

  protected toggleStackTrace(): void {
    this.stackTraceOpen.update((open) => !open);
  }
}
