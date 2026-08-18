import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { DeploymentRegistry } from '../contracts/deployment-registry';
import { DeploymentRegistryDataSource } from './deployment-registry.data-source';

/**
 * Reads recorded release history.
 *
 * Records are written when a project is refreshed, so this endpoint never calls GitHub itself and the
 * screen shows what Console Ops has observed rather than a live provider query.
 */
@Injectable()
export class HttpDeploymentRegistryDataSource extends DeploymentRegistryDataSource {
  private readonly http = inject(HttpClient);

  override load(): Observable<DeploymentRegistry> {
    return this.http.get<DeploymentRegistry>('/api/deployments');
  }
}
