import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';

import { EnvironmentRegistry } from '../../contracts/environment-registry';
import { EnvironmentRegistryDataSource } from '../environment-registry.data-source';
import { ENVIRONMENT_REGISTRY_FIXTURE } from './environment-registry.fixture';

/**
 * Design-stage adapter for the environment registry port.
 *
 * Temporary: it exists only until the environment query slice lands, at which point it is deleted
 * rather than kept as a runtime fallback.
 */
@Injectable()
export class MockEnvironmentRegistryDataSource extends EnvironmentRegistryDataSource {
  override load(): Observable<EnvironmentRegistry> {
    return of(ENVIRONMENT_REGISTRY_FIXTURE);
  }
}
