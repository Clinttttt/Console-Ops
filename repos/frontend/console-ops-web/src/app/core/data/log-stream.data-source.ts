import { Observable } from 'rxjs';

import { LogStream } from '../contracts/log-stream';

/** Which scope to read, and what to narrow it to on the provider side. */
export interface LogStreamRequest {
  readonly projectId: string | null;
  readonly environmentId: string | null;
  /**
   * Free text pushed down to the provider, because a window can hold far more lines than a page. Filtering
   * only what was already fetched would silently hide matches further back in the window.
   */
  readonly search: string | null;
  /**
   * Read the window ending at this instant instead of now, which is how the screen pages backwards.
   *
   * The cursor is a time rather than an id because that is what the provider can seek on. Lines can share
   * an instant, so a page is merged by id rather than trusted to start cleanly after the previous one: an
   * exclusive time cursor would drop every line that shared the boundary millisecond.
   */
  readonly before: string | null;
  /**
   * Read only what has happened since this instant, which is how the screen follows a scope while `Live` is
   * on. A tail asks for seconds instead of a day, so following a stream costs a fraction of re-reading it.
   */
  readonly since: string | null;
  /**
   * Keep framework chatter in the stream. Off by default: an idle service logs almost nothing else, and the
   * lines the operator came for are buried under it. Filtering is pushed down for the same reason search is
   * - the window holds far more lines than a page, so removing noise locally would just empty the page.
   */
  readonly includeNoise: boolean;
}

/**
 * Port for reading the log stream.
 *
 * The UI depends only on this abstraction, and Console Ops holds the provider credential: the browser never
 * queries a log provider itself.
 */
export abstract class LogStreamDataSource {
  abstract load(request: LogStreamRequest): Observable<LogStream>;
}
