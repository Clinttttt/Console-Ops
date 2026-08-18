import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { DeploymentRegistry } from '../contracts/deployment-registry';
import { HttpDeploymentRegistryDataSource } from './http-deployment-registry.data-source';

const REGISTRY: DeploymentRegistry = {
  observedAt: '2026-08-14T09:30:00Z',
  deployments: [
    {
      id: 'deployment-1',
      projectId: 'project-1',
      projectName: 'Spinner API',
      provider: 'githubActions',
      repository: 'spinner/api',
      branch: 'main',
      commitSha: '8a17c2f9abcdef0123456789abcdef0123456789',
      commitShortSha: '8a17c2f',
      result: 'passed',
      workflowFile: 'deploy.yml',
      workflowName: 'Deploy',
      workflowUrl: 'https://github.com/spinner/api/actions/runs/1421',
      runNumber: 1421,
      triggeredBy: 'ci-bot',
      startedAt: '2026-08-14T09:09:42Z',
      completedAt: '2026-08-14T09:12:00Z',
      deployedAt: '2026-08-14T09:12:00Z',
      durationSeconds: 138,
      recordedAt: '2026-08-14T09:15:00Z',
      environments: [],
    },
  ],
};

describe('HttpDeploymentRegistryDataSource', () => {
  it('reads recorded release history from the backend contract endpoint', () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        HttpDeploymentRegistryDataSource,
      ],
    });
    const dataSource = TestBed.inject(HttpDeploymentRegistryDataSource);
    const http = TestBed.inject(HttpTestingController);
    let actual = null;

    dataSource.load().subscribe((registry) => (actual = registry));
    const request = http.expectOne('/api/deployments');
    expect(request.request.method).toBe('GET');
    request.flush(REGISTRY);

    expect(actual).toEqual(REGISTRY);
    http.verify();
  });
});
