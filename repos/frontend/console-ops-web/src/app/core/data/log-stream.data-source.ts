import { Observable } from 'rxjs';

import { LogStream } from '../contracts/log-stream';

/**
 * Port for reading the log stream.
 *
 * The UI depends only on this abstraction. A design-stage mock adapter backs it today; an HTTP adapter
 * replaces it at the provider registration in `app.config.ts` when log ingestion exists, and the mock is
 * then deleted rather than kept as a fallback.
 */
export abstract class LogStreamDataSource {
  abstract load(): Observable<LogStream>;
}
