import { Observable } from 'rxjs';

import { DashboardOverview } from '../contracts/dashboard-overview';

/**
 * Port for reading the dashboard overview.
 *
 * The UI depends only on this abstraction. V1 registers a mock adapter; the HTTP adapter for
 * `GET /api/dashboard/overview` replaces it at the provider registration in `app.config.ts` without
 * touching a single component.
 */
export abstract class DashboardOverviewDataSource {
  abstract load(): Observable<DashboardOverview>;
}
