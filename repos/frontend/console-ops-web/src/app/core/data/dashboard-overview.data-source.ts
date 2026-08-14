import { Observable } from 'rxjs';

import { DashboardOverview } from '../contracts/dashboard-overview';

/**
 * Port for reading the dashboard overview.
 *
 * The UI depends only on this abstraction. The runtime provider reads
 * `GET /api/dashboard/overview`; tests can replace the port without changing components.
 */
export abstract class DashboardOverviewDataSource {
  abstract load(): Observable<DashboardOverview>;
}
