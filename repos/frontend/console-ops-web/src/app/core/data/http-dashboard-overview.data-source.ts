import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { DashboardOverview } from '../contracts/dashboard-overview';
import { DashboardOverviewDataSource } from './dashboard-overview.data-source';

/** Reads the persisted V1 overview; provider calls happen only through explicit project refreshes. */
@Injectable()
export class HttpDashboardOverviewDataSource extends DashboardOverviewDataSource {
  private readonly http = inject(HttpClient);

  override load(): Observable<DashboardOverview> {
    return this.http.get<DashboardOverview>('/api/dashboard/overview');
  }
}
