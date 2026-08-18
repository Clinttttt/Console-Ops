import { Observable } from 'rxjs';

import { HealthSnapshot } from '../contracts/health';

/**
 * Port for the Health screen.
 *
 * A read only: Health reports recorded checks, and collection is the worker's job. Nothing on this screen asks
 * a provider during the request.
 */
export abstract class HealthDataSource {
  abstract load(): Observable<HealthSnapshot>;
}
