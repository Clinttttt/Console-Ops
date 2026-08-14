import { Observable } from 'rxjs';

import { DeploymentRegistry } from '../contracts/deployment-registry';

/**
 * Port for reading deployment history.
 *
 * The UI depends only on this abstraction. A design-stage mock adapter backs it today; an HTTP adapter
 * replaces it at the provider registration in `app.config.ts` when the deployment-history phase lands.
 */
export abstract class DeploymentRegistryDataSource {
  abstract load(): Observable<DeploymentRegistry>;
}
