import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

import {
  LogEvent,
  LogMarker,
  LogStreamItem,
  LogStreamWindow,
} from '../../../core/contracts/log-stream';
import { Icon } from '../../../core/ui/icon';
import { LogMarkerRow } from './log-marker';

/** One UTC day of the stream. Grouping keeps a long stream orientated without repeating the date. */
interface StreamDay {
  /** `YYYY-MM-DD` in UTC, used as the group key. */
  readonly key: string;
  /**
   * A full instant from the group. `DatePipe` reads a date-only string as local time, which can shift the
   * rendered day, so the label is formatted from a real instant in UTC instead.
   */
  readonly instant: string;
  readonly isToday: boolean;
  readonly items: readonly LogStreamItem[];
}

/**
 * The log stream: application, runtime, and platform events in the order they happened.
 *
 * Each line shows only what can be scanned - time, severity, source, message - and everything else waits
 * behind selection. A wall of properties or a stack trace in the stream would make it unreadable, which is
 * the failure mode this screen has to avoid.
 *
 * Markers are the one piece of cross-screen context allowed here. They explain a change in what follows:
 * errors that begin immediately after a release are the reason this screen exists.
 */
@Component({
  selector: 'co-log-stream',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, Icon, LogMarkerRow],
  templateUrl: './log-stream.html',
  styleUrl: './log-stream.scss',
})
export class LogStreamView {
  readonly items = input.required<readonly LogStreamItem[]>();
  readonly totalCount = input.required<number>();
  readonly errorCount = input.required<number>();
  readonly selectedId = input<string | null>(null);
  readonly observedAt = input<string | null>(null);
  readonly live = input(false);
  readonly window = input<LogStreamWindow | null>(null);

  readonly selectEvent = output<string>();
  readonly clearFilters = output<void>();

  protected readonly days = computed<readonly StreamDay[]>(() => {
    const observedDay = this.observedAt()?.slice(0, 10) ?? null;
    const groups = new Map<string, LogStreamItem[]>();

    for (const item of this.items()) {
      const key = item.occurredAt.slice(0, 10);
      const bucket = groups.get(key);
      if (bucket === undefined) {
        groups.set(key, [item]);
      } else {
        bucket.push(item);
      }
    }

    return [...groups.entries()]
      .sort(([left], [right]) => left.localeCompare(right))
      .map(([key, items]) => ({
        key,
        instant: items[0].occurredAt,
        isToday: key === observedDay,
        items: [...items].sort((left, right) => left.occurredAt.localeCompare(right.occurredAt)),
      }));
  });

  /** How many events are in view, ignoring markers, which are context rather than records. */
  protected readonly eventCount = computed(
    () => this.items().filter((item) => item.kind === 'event').length,
  );

  protected isEvent(item: LogStreamItem): item is LogEvent {
    return item.kind === 'event';
  }

  protected asEvent(item: LogStreamItem): LogEvent {
    return item as LogEvent;
  }

  protected asMarker(item: LogStreamItem): LogMarker {
    return item as LogMarker;
  }

  /** Three letters, as a log stream is normally read. An unparsed line is not called information. */
  protected levelLabel(event: LogEvent): string {
    switch (event.level) {
      case 'trace':
        return 'TRC';
      case 'debug':
        return 'DBG';
      case 'information':
        return 'INF';
      case 'warning':
        return 'WRN';
      case 'error':
        return 'ERR';
      case 'critical':
        return 'CRT';
      default:
        return 'LOG';
    }
  }

  /**
   * Severity mapped onto the shared status levels that drive `.co-dot`.
   *
   * The design system speaks in operational levels, not log levels, so an event has to be translated or it
   * renders as `unknown` grey. Doing it here keeps one meaning of green, amber, and red across every screen.
   */
  protected dotLevel(event: LogEvent): 'healthy' | 'warning' | 'down' | 'unknown' {
    switch (event.level) {
      case 'error':
      case 'critical':
        return 'down';
      case 'warning':
        return 'warning';
      case 'information':
        return 'healthy';
      default:
        return 'unknown';
    }
  }

  /**
   * The short trailing context. Console output carries no status code or duration, so the only real signal
   * left is the stream it was written to; anything else here would be invented.
   */
  protected trailing(event: LogEvent): string | null {
    return event.stream === 'stderr' ? 'stderr' : null;
  }
}
