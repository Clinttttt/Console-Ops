import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';

import { ProjectRegistry } from '../../contracts/project-registry';
import { ProjectRegistryDataSource } from '../project-registry.data-source';
import { PROJECT_REGISTRY_FIXTURE } from './project-registry.fixture';

/**
 * Design-stage adapter for the project registry port.
 *
 * Temporary: it exists only until the project list query slice lands, at which point it is deleted
 * rather than kept as a runtime fallback.
 */
@Injectable()
export class MockProjectRegistryDataSource extends ProjectRegistryDataSource {
  override load(): Observable<ProjectRegistry> {
    return of(PROJECT_REGISTRY_FIXTURE);
  }
}
