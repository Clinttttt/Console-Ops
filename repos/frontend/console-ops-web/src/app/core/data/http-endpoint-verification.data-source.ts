import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  EndpointVerification,
  EndpointVerificationRequest,
} from '../contracts/endpoint-verification';
import { EndpointVerificationDataSource } from './endpoint-verification.data-source';

/** Asks the API to probe candidate endpoints. The browser never probes a target application itself. */
@Injectable()
export class HttpEndpointVerificationDataSource extends EndpointVerificationDataSource {
  private readonly http = inject(HttpClient);

  override verify(request: EndpointVerificationRequest): Observable<EndpointVerification> {
    return this.http.post<EndpointVerification>('/api/projects/verification', request);
  }
}
