import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

import { LogEvent, LogMarker, LogStreamItem } from '../../../core/contracts/log-stream';
import { Icon } from '../../../core/ui/icon';
import { LogMarkerRow } from './log-marker';

/** One UTC day of the stream. Grouping keeps a long stream orientated without repeating the date. */
interface StreamDay {
  /** `YYYY-MM-DD` in UTC, used as the group key. */
  readonly key: string;
  /**
   * A full instant from the group. `DatePipe` reads a date-only string as local time, which can shift
   * the rendered day, so the label is formatted from a real instant in UTC instead.
   */
  readonly instant: string;
  readonly isToday: boolean;
  readonly items: readonly LogStreamItem[];
}

/**
 * The log stream: application, runtime, and platform events in the order they happened.
 *
 * Each line shows only what can be scanned - time, severity, source, message, and a short outcome - and
 * everything else waits behind selection. A wall of properties or a stack trace in the stream would make
 * it unreadable, which is the failure mode this screen has to avoid.
 *
 * Markers are the one piece of cross-screen context allowed here. They explain a change in what follows:
 * errors that begin immediately after a release are the reason this screen exists, so a deployment marker
 * links back to that release rather than repeating it.
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

  /** Three-letter severity, as a log stream is normally read. */
  protected levelLabel(event: LogEvent): string {
    switch (event.level) {
      case 'error':
        return 'ERR';
      case 'warning':
        return 'WRN';
      default:
        return 'INF';
    }
  }

  /**
   * Severity mapped onto the shared status levels that drive `.co-dot`.
   *
   * The design system speaks in operational levels, not log levels, so an event has to be translated or
   * it renders as `unknown` grey. Doing it here keeps one meaning of green, amber, and red across every
   * screen.
   */
  protected dotLevel(event: LogEvent): 'healthy' | 'warning' | 'down' {
    switch (event.level) {
      case 'error':
        return 'down';
      case 'warning':
        return 'warning';
      default:
        return 'healthy';
    }
  }

  /**
   * The short trailing context, composed here rather than sent by the contract: a status code, a
   * duration, or the most telling property. `null` when the event reported nothing worth a suffix.
   */
  protected outcomeLabel(event: LogEvent): string | null {
    const parts: string[] = [];
    const outcome = event.outcome;

    if (outcome?.statusCode !== null && outcome?.statusCode !== undefined) {
      parts.push(`${outcome.statusCode}`);
    }

    if (outcome?.durationMs !== null && outcome?.durationMs !== undefined) {
      parts.push(formatDuration(outcome.durationMs));
    }

    if (parts.length === 0) {
      const property = event.properties[0];
      return property === undefined ? null : `${property.name} ${property.value}`;
    }

    return parts.join(' \u00b7 ');
  }
}

function formatDuration(milliseconds: number): string {
  if (milliseconds < 1000) {
    return `${Math.round(milliseconds)} ms`;
  }

  return `${(milliseconds / 1000).toFixed(milliseconds < 10_000 ? 2 : 1)} s`;
}
