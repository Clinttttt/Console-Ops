import { Observable } from 'rxjs';

import { EnvironmentRegistry } from '../contracts/environment-registry';

/**
 * Port for reading environments across projects.
 *
 * The UI depends only on this abstraction. A design-stage mock adapter backs it today; the HTTP
 * adapter replaces it at the provider registration in `app.config.ts` without touching a component.
 */
export abstract class EnvironmentRegistryDataSource {
  abstract load(): Observable<EnvironmentRegistry>;
}
