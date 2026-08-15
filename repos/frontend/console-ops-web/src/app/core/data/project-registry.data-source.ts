import { Observable } from 'rxjs';

import { ProjectRegistrationRequest } from '../contracts/project-registration';
import { ProjectUpdateRequest } from '../contracts/project-update';
import { ProjectListItem, ProjectRegistry } from '../contracts/project-registry';

/**
 * Narrow port for the V1 project resource and its explicit observation refresh operation.
 */
export abstract class ProjectRegistryDataSource {
  abstract load(): Observable<ProjectRegistry>;
  abstract getProject(projectId: string): Observable<ProjectListItem>;
  abstract register(request: ProjectRegistrationRequest): Observable<ProjectListItem>;
  abstract updateProject(
    projectId: string,
    request: ProjectUpdateRequest,
  ): Observable<ProjectListItem>;
  /** Soft archive: the project stops being monitored and leaves normal queries. */
  abstract archiveProject(projectId: string): Observable<unknown>;
  abstract refreshProject(projectId: string): Observable<unknown>;
}
