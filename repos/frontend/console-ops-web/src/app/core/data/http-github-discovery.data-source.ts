import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { GitHubRepositoryPage, GitHubWorkflowList } from '../contracts/github-discovery';
import { GitHubDiscoveryDataSource } from './github-discovery.data-source';

/**
 * Reads GitHub discovery endpoints.
 *
 * These endpoints are planned but not implemented, so this adapter is expected to fail today. That is
 * deliberate: the UI reports discovery as unavailable and keeps manual entry working, and the import
 * path starts functioning the moment the API ships without any frontend change.
 */
@Injectable()
export class HttpGitHubDiscoveryDataSource extends GitHubDiscoveryDataSource {
  private readonly http = inject(HttpClient);

  override listRepositories(query: string): Observable<GitHubRepositoryPage> {
    const trimmed = query.trim();
    const params = trimmed === '' ? undefined : new HttpParams({ fromObject: { query: trimmed } });

    return this.http.get<GitHubRepositoryPage>('/api/github/repositories', { params });
  }

  override listWorkflows(owner: string, name: string): Observable<GitHubWorkflowList> {
    const path = `/api/github/repositories/${encodeURIComponent(owner)}/${encodeURIComponent(name)}/workflows`;
    return this.http.get<GitHubWorkflowList>(path);
  }
}
