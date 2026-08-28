import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { ProjectRegistrationRequest } from '../contracts/project-registration';
import { PROJECT_REGISTRY_FIXTURE } from './mock/project-registry.fixture';
import { HttpProjectRegistryDataSource } from './http-project-registry.data-source';

describe('HttpProjectRegistryDataSource', () => {
  let dataSource: HttpProjectRegistryDataSource;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), HttpProjectRegistryDataSource],
    });
    dataSource = TestBed.inject(HttpProjectRegistryDataSource);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads active project resources', () => {
    let actual = null;
    dataSource.load().subscribe((projects) => (actual = projects));

    const request = http.expectOne('/api/projects');
    expect(request.request.method).toBe('GET');
    request.flush(PROJECT_REGISTRY_FIXTURE);
    expect(actual).toEqual(PROJECT_REGISTRY_FIXTURE);
  });

  it('registers the exact V1 request body', () => {
    const registration: ProjectRegistrationRequest = {
      name: 'Spinner API',
      description: null,
      repository: {
        owner: 'spinner',
        name: 'api',
        defaultBranch: 'main',
        workflowFile: 'deploy.yml',
      },
      environments: [
        {
          name: 'Production',
          kind: 'production',
          applicationUrl: 'https://api.spinner.example',
          healthUrl: 'https://api.spinner.example/health',
          versionUrl: 'https://api.spinner.example/version',
          logSource: {
            workspaceId: '6f5c1a2b-3d4e-5f60-7182-93a4b5c6d7e8',
            containerAppName: 'spinner-api',
            platform: 'containerApp',
          },
        },
      ],
    };

    dataSource.register(registration).subscribe();
    const request = http.expectOne('/api/projects');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(registration);
    request.flush(PROJECT_REGISTRY_FIXTURE[0]);
  });

  it('requests an explicit observation refresh for the registered project', () => {
    dataSource.refreshProject('project-id').subscribe();
    const request = http.expectOne('/api/projects/project-id/refresh');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toBeNull();
    request.flush({});
  });
});
