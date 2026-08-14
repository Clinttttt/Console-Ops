import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ProjectRegistrationRequest } from '../contracts/project-registration';
import { ProjectListItem, ProjectRegistry } from '../contracts/project-registry';
import { ProjectRegistryDataSource } from './project-registry.data-source';

/** Uses the persisted V1 project resource. Refresh is explicit because reads never call providers. */
@Injectable()
export class HttpProjectRegistryDataSource extends ProjectRegistryDataSource {
  private readonly http = inject(HttpClient);

  override load(): Observable<ProjectRegistry> {
    return this.http.get<ProjectRegistry>('/api/projects');
  }

  override register(request: ProjectRegistrationRequest): Observable<ProjectListItem> {
    return this.http.post<ProjectListItem>('/api/projects', request);
  }

  override refreshProject(projectId: string): Observable<unknown> {
    return this.http.post(`/api/projects/${encodeURIComponent(projectId)}/refresh`, null);
  }
}
