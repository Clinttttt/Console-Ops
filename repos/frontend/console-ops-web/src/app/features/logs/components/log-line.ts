import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

import { LogEvent } from '../../../core/contracts/log-stream';
import { shortenCategory } from './log-source-label';

/**
 * One scannable line of the stream.
 *
 * Its own component for a measurable reason: selection is a signal on the page, so without a component
 * boundary here every line in the stream re-evaluates its bindings each time an event is chosen. With
 * hundreds of lines paged in that made a click feel slow. As an `OnPush` component with signal inputs, only
 * the line that lost the selection and the one that gained it re-render.
 *
 * Presentation is computed once per line rather than called from the template for the same reason.
 */
@Component({
  selector: 'co-log-line',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe],
  templateUrl: './log-line.html',
  styleUrl: './log-line.scss',
})
export class LogLine {
  readonly event = input.required<LogEvent>();
  readonly isSelected = input(false);

  /** Named for the concept, not the DOM: an output called select shadows the native select event. */
  readonly selectEvent = output<string>();

  /** Three letters, as a log stream is normally read. An unparsed line is not called information. */
  protected readonly levelLabel = computed(() => {
    switch (this.event().level) {
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
  });

  /**
   * Severity mapped onto the shared status levels that drive `.co-dot`.
   *
   * The design system speaks in operational levels, not log levels, so an event has to be translated or it
   * renders as `unknown` grey. Doing it here keeps one meaning of green, amber, and red across every screen.
   */
  protected readonly dotLevel = computed<'healthy' | 'warning' | 'down' | 'unknown'>(() => {
    switch (this.event().level) {
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
  });

  /**
   * The readable end of an emitter category. The full value stays on the tooltip and in the detail rail.
   */
  protected readonly sourceLabel = computed(() => {
    const source = this.event().source;
    return source === null ? null : shortenCategory(source);
  });

  /**
   * The message as the stream shows it.
   *
   * .NET writes some lines for machines rather than for reading. A request line repeats the protocol, the
   * scheme, and a host that is the same for every line in the scope, and buries the method, path, status and
   * duration inside it. Where the shape is recognized the line is composed down to what distinguishes it,
   * and the status and duration move to the trailing column - which is what that column is for.
   *
   * Nothing is invented and nothing is lost: every value shown is read out of the line, and the detail rail
   * still shows the provider's text exactly as it arrived. An unrecognized message is left alone.
   */
  protected readonly displayMessage = computed(
    () => summarize(this.event().message)?.summary ?? this.event().message,
  );

  /**
   * The short trailing context: a status code and duration when the line carried them, otherwise the stream
   * it was written to. Console output has no outcome column of its own, so anything beyond this would be
   * invented.
   */
  protected readonly trailing = computed(() => {
    const composed = summarize(this.event().message)?.outcome ?? null;
    if (composed !== null) {
      return composed;
    }

    return this.event().stream === 'stderr' ? 'stderr' : null;
  });
}

/** A recognized message shape, reduced to what tells one line from another. */
interface Summary {
  readonly summary: string;
  readonly outcome: string | null;
}

/** `Request finished HTTP/1.1 GET https://host/path?q - 200 - text/json 424.8084ms` */
const REQUEST = /^Request (starting|finished) HTTP\/[\d.]+ ([A-Z]+) (\S+)(.*)$/;

/** `Executed DbCommand (3ms) [Parameters=[...], CommandType='Text', CommandTimeout='30']` */
const DB_COMMAND = /^(Executed|Failed executing) DbCommand \(([\d.]+)ms\)/;

function summarize(message: string): Summary | null {
  const request = REQUEST.exec(message);
  if (request !== null) {
    const [, phase, method, url, rest] = request;
    const status = /-\s(\d{3})(?:\s|$)/.exec(rest)?.[1] ?? null;
    const milliseconds = /([\d.]+)ms\s*$/.exec(rest)?.[1] ?? null;
    const parts = [status, milliseconds === null ? null : formatMilliseconds(milliseconds)].filter(
      (part): part is string => part !== null,
    );

    return {
      summary: `${method} ${pathOf(url)}`,
      // A request that only started has no outcome yet, and saying so is the point of the line.
      outcome: parts.length > 0 ? parts.join(' · ') : phase === 'starting' ? 'started' : null,
    };
  }

  const command = DB_COMMAND.exec(message);
  if (command !== null) {
    const [, verb, milliseconds] = command;
    return {
      // The parameter list is a row of '?' placeholders; the SQL itself is in the folded continuation.
      summary: verb === 'Executed' ? 'Executed DbCommand' : 'Failed executing DbCommand',
      outcome: formatMilliseconds(milliseconds),
    };
  }

  return null;
}

/** Path and query only. The scheme and host are the same for every line in a scope. */
function pathOf(url: string): string {
  const scheme = url.indexOf('://');
  if (scheme < 0) {
    return url;
  }

  const path = url.indexOf('/', scheme + 3);
  return path < 0 ? '/' : url.slice(path);
}

/** Whole milliseconds once there are some, one decimal below that, so sub-millisecond work is not "0 ms". */
function formatMilliseconds(value: string): string {
  const milliseconds = Number(value);
  if (!Number.isFinite(milliseconds)) {
    return `${value} ms`;
  }

  return milliseconds >= 10
    ? `${Math.round(milliseconds)} ms`
    : `${Math.round(milliseconds * 10) / 10} ms`;
}
