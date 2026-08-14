import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';

import { DeploymentRegistry } from '../../contracts/deployment-registry';
import { DeploymentRegistryDataSource } from '../deployment-registry.data-source';
import { DEPLOYMENT_REGISTRY_FIXTURE } from './deployment-registry.fixture';

/**
 * Design-stage adapter for the deployment registry port.
 *
 * Temporary: it exists only until the deployment-history phase lands, at which point it is deleted
 * rather than kept as a runtime fallback.
 */
@Injectable()
export class MockDeploymentRegistryDataSource extends DeploymentRegistryDataSource {
  override load(): Observable<DeploymentRegistry> {
    return of(DEPLOYMENT_REGISTRY_FIXTURE);
  }
}
