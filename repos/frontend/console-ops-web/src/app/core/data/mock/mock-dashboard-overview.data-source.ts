import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';

import { DashboardOverview } from '../../contracts/dashboard-overview';
import { DashboardOverviewDataSource } from '../dashboard-overview.data-source';
import { DASHBOARD_OVERVIEW_FIXTURE } from './dashboard-overview.fixture';

/**
 * Mock adapter for the dashboard overview port.
 *
 * Temporary: it exists only until the backend dashboard query slice lands, at which point it is
 * removed rather than kept as a runtime fallback.
 */
@Injectable()
export class MockDashboardOverviewDataSource extends DashboardOverviewDataSource {
  override load(): Observable<DashboardOverview> {
    return of(DASHBOARD_OVERVIEW_FIXTURE);
  }
}
