import { Observable } from 'rxjs';

import { ProjectRegistrationRequest } from '../contracts/project-registration';
import { ProjectListItem, ProjectRegistry } from '../contracts/project-registry';

/**
 * Narrow port for the V1 project resource and its explicit observation refresh operation.
 */
export abstract class ProjectRegistryDataSource {
  abstract load(): Observable<ProjectRegistry>;
  abstract register(request: ProjectRegistrationRequest): Observable<ProjectListItem>;
  abstract refreshProject(projectId: string): Observable<unknown>;
}
