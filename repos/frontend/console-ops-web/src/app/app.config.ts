import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router';

import { routes } from './app.routes';
import { DashboardOverviewDataSource } from './core/data/dashboard-overview.data-source';
import { DeploymentRegistryDataSource } from './core/data/deployment-registry.data-source';
import { EndpointVerificationDataSource } from './core/data/endpoint-verification.data-source';
import { EnvironmentRegistryDataSource } from './core/data/environment-registry.data-source';
import { GitHubDiscoveryDataSource } from './core/data/github-discovery.data-source';
import { HttpDashboardOverviewDataSource } from './core/data/http-dashboard-overview.data-source';
import { HttpEndpointVerificationDataSource } from './core/data/http-endpoint-verification.data-source';
import { HttpGitHubDiscoveryDataSource } from './core/data/http-github-discovery.data-source';
import { HttpProjectRegistryDataSource } from './core/data/http-project-registry.data-source';
import { MockDeploymentRegistryDataSource } from './core/data/mock/mock-deployment-registry.data-source';
import { MockEnvironmentRegistryDataSource } from './core/data/mock/mock-environment-registry.data-source';
import { ProjectRegistryDataSource } from './core/data/project-registry.data-source';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(),
    provideRouter(
      routes,
      withComponentInputBinding(),
      withInMemoryScrolling({ scrollPositionRestoration: 'top' }),
    ),
    { provide: DashboardOverviewDataSource, useClass: HttpDashboardOverviewDataSource },
    { provide: ProjectRegistryDataSource, useClass: HttpProjectRegistryDataSource },

    // Phases 1 to 3 of the Add Project import plan: repository and workflow discovery, and probing
    // candidate endpoints before registration. All three are implemented in the API.
    { provide: GitHubDiscoveryDataSource, useClass: HttpGitHubDiscoveryDataSource },
    { provide: EndpointVerificationDataSource, useClass: HttpEndpointVerificationDataSource },

    // Design stage: the Environments screen runs on a fixture because the cross-project environment
    // read model in `core/contracts/environment-registry.ts` still exceeds the frozen V1 contract.
    // Replace this one provider with an HTTP adapter once that query exists, then delete the mock.
    { provide: EnvironmentRegistryDataSource, useClass: MockEnvironmentRegistryDataSource },

    // Design stage: the Deployments screen runs on a fixture because deployment history is a later
    // product phase. Replace this provider with an HTTP adapter when that phase lands, then delete
    // the mock.
    { provide: DeploymentRegistryDataSource, useClass: MockDeploymentRegistryDataSource },
  ],
};
