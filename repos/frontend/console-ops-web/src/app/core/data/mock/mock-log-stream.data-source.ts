import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';

import { LogStream } from '../../contracts/log-stream';
import { LogStreamDataSource } from '../log-stream.data-source';
import { LOG_STREAM_FIXTURE } from './log-stream.fixture';

/**
 * Design-stage adapter for the log stream port.
 *
 * Temporary: it exists only until log ingestion lands, at which point it is deleted rather than kept as
 * a runtime fallback.
 */
@Injectable()
export class MockLogStreamDataSource extends LogStreamDataSource {
  override load(): Observable<LogStream> {
    return of(LOG_STREAM_FIXTURE);
  }
}
