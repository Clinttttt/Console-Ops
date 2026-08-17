import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  computed,
  inject,
  input,
  output,
  viewChild,
} from '@angular/core';

import {
  LogEvent,
  LogMarker,
  LogStreamItem,
  LogStreamNoise,
  LogStreamWindow,
} from '../../../core/contracts/log-stream';
import { LogStreamOlderPages } from '../../../core/state/log-stream.store';
import { Icon } from '../../../core/ui/icon';
import { LogMarkerRow } from './log-marker';
import { LogStreamEmpty } from './log-empty';
import { LogLine } from './log-line';

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
  imports: [DatePipe, Icon, LogLine, LogMarkerRow, LogStreamEmpty],
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
  readonly olderPages = input<LogStreamOlderPages>('unknown');
  readonly noise = input<LogStreamNoise | null>(null);
  /**
   * Whether the operator has narrowed the view. An empty stream means something different when a filter is
   * set, and the screen should not blame a quiet service for a narrow filter.
   */
  readonly filtersActive = input(false);

  readonly selectEvent = output<string>();
  readonly clearFilters = output<void>();
  readonly loadOlder = output<void>();
  readonly toggleNoise = output<void>();

  /**
   * The row that offers the previous window, watched so that scrolling to the top of the stream reads it
   * without a click.
   *
   * The observer is created **once**. Rebuilding it whenever the row changed re-reported "visible" on every
   * rebuild, which paged backwards in a loop: reading a page changed the row, the row rebuilt the observer,
   * and the observer asked for another page. The row is a real button as well, because an observer-only
   * affordance cannot be reached from the keyboard, and the browser's own scroll anchoring keeps the view
   * steady when lines are added above.
   */
  private readonly olderTrigger = viewChild<ElementRef<HTMLElement>>('olderTrigger');
  private readonly destroyRef = inject(DestroyRef);

  constructor() {
    afterNextRender(() => {
      const trigger = this.olderTrigger()?.nativeElement;
      if (trigger === undefined || typeof IntersectionObserver === 'undefined') {
        return;
      }

      const observer = new IntersectionObserver((entries) => {
        if (entries.some((entry) => entry.isIntersecting) && this.olderPages() === 'available') {
          this.loadOlder.emit();
        }
      });
      observer.observe(trigger);
      this.destroyRef.onDestroy(() => observer.disconnect());
    });
  }

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

    return (
      [...groups.entries()]
        // Newest first, both between days and within a day: the operator opens this screen to see what just
        // happened, and a stream that starts a day behind makes them scroll to find it.
        .sort(([left], [right]) => right.localeCompare(left))
        .map(([key, items]) => ({
          key,
          instant: items[0].occurredAt,
          isToday: key === observedDay,
          items: [...items].sort((left, right) => {
            const byTime = right.occurredAt.localeCompare(left.occurredAt);
            if (byTime !== 0) {
              return byTime;
            }

            // Same instant: the event sits above the marker, so the marker reads as the explanation for
            // everything above it. Matches the order the API composes, whatever order a page arrived in.
            return (left.kind === 'marker' ? 1 : 0) - (right.kind === 'marker' ? 1 : 0);
          }),
        }))
        // A day of nothing but markers has nothing to explain. Markers are context for the lines around
        // them, so a date header carrying only releases is noise rather than information.
        .filter((day) => day.items.some((item) => item.kind === 'event'))
    );
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
}
