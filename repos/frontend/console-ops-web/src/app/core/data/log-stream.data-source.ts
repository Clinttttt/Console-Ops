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
