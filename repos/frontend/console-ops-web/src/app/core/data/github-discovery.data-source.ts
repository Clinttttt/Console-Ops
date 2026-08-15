import { Observable } from 'rxjs';

import {
  GitHubLatestCommit,
  GitHubRepositoryPage,
  GitHubWorkflowList,
} from '../contracts/github-discovery';

/**
 * Port for GitHub discovery.
 *
 * Discovery is an optional capability: the API may not implement it yet, and a project may have no
 * connected repository at all. Callers must handle failure by falling back to manual entry rather than
 * blocking registration.
 */
export abstract class GitHubDiscoveryDataSource {
  abstract listRepositories(query: string): Observable<GitHubRepositoryPage>;

  abstract listWorkflows(owner: string, name: string): Observable<GitHubWorkflowList>;

  abstract getLatestCommit(
    owner: string,
    name: string,
    branch: string,
  ): Observable<GitHubLatestCommit>;
}
