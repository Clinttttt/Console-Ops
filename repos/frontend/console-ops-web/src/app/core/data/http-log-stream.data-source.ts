import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { LogStream } from '../contracts/log-stream';
import { LogStreamDataSource, LogStreamRequest } from './log-stream.data-source';

/**
 * Reads one scope's logs from Console Ops, which reads the provider.
 *
 * The browser never talks to Azure: it has no credential and should not have one.
 */
@Injectable()
export class HttpLogStreamDataSource extends LogStreamDataSource {
  private readonly http = inject(HttpClient);

  override load(request: LogStreamRequest): Observable<LogStream> {
    const params: Record<string, string> = {};
    if (request.projectId !== null) {
      params['projectId'] = request.projectId;
    }
    if (request.environmentId !== null) {
      params['environmentId'] = request.environmentId;
    }
    if (request.search !== null && request.search.trim() !== '') {
      params['search'] = request.search.trim();
    }

    return this.http.get<LogStream>('/api/logs', { params });
  }
}
