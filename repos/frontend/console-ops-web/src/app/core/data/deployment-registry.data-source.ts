import { Observable } from 'rxjs';

import { DeploymentRegistry } from '../contracts/deployment-registry';

/**
 * Port for reading deployment history.
 *
 * The UI depends only on this abstraction; the HTTP adapter is bound at the provider registration in
 * `app.config.ts`.
 */
export abstract class DeploymentRegistryDataSource {
  abstract load(): Observable<DeploymentRegistry>;
}
