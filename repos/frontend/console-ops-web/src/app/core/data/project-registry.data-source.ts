import { Observable } from 'rxjs';

import { ProjectRegistry } from '../contracts/project-registry';

/**
 * Port for reading the project registry.
 *
 * The UI depends only on this abstraction. A design-stage mock adapter backs it today; the HTTP
 * adapter for the real list query replaces it at the provider registration in `app.config.ts`
 * without touching a component.
 */
export abstract class ProjectRegistryDataSource {
  abstract load(): Observable<ProjectRegistry>;
}
