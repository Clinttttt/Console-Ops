import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { AzureLogSources } from '../contracts/azure-discovery';

/**
 * Port for Azure discovery.
 *
 * The browser never talks to Azure: it asks Console Ops, which holds the credential and does the read.
 */
export abstract class AzureDiscoveryDataSource {
  abstract listLogSources(query: string | null): Observable<AzureLogSources>;
}

@Injectable()
export class HttpAzureDiscoveryDataSource extends AzureDiscoveryDataSource {
  private readonly http = inject(HttpClient);

  override listLogSources(query: string | null): Observable<AzureLogSources> {
    const params = query === null || query.trim() === '' ? {} : { params: { query: query.trim() } };
    return this.http.get<AzureLogSources>('/api/azure/log-sources', params);
  }
}
