import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router';

import { routes } from './app.routes';
import {
  AzureDiscoveryDataSource,
  HttpAzureDiscoveryDataSource,
} from './core/data/azure-discovery.data-source';
import { DashboardOverviewDataSource } from './core/data/dashboard-overview.data-source';
import { DeploymentRegistryDataSource } from './core/data/deployment-registry.data-source';
import { EndpointVerificationDataSource } from './core/data/endpoint-verification.data-source';
import { GitHubDiscoveryDataSource } from './core/data/github-discovery.data-source';
import { HttpDashboardOverviewDataSource } from './core/data/http-dashboard-overview.data-source';
import { HttpDeploymentRegistryDataSource } from './core/data/http-deployment-registry.data-source';
import { HttpEndpointVerificationDataSource } from './core/data/http-endpoint-verification.data-source';
import { HttpGitHubDiscoveryDataSource } from './core/data/http-github-discovery.data-source';
import { HttpLogStreamDataSource } from './core/data/http-log-stream.data-source';
import { HttpProjectRegistryDataSource } from './core/data/http-project-registry.data-source';
import { LogStreamDataSource } from './core/data/log-stream.data-source';
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

    // Azure discovery for the log-source picker: read-only listing of container apps and their workspace.
    { provide: AzureDiscoveryDataSource, useClass: HttpAzureDiscoveryDataSource },

    // Release history recorded from GitHub Actions runs during project refreshes.
    { provide: DeploymentRegistryDataSource, useClass: HttpDeploymentRegistryDataSource },

    // Application logs read from each environment's provider. Console Ops holds the credential; the
    // browser only asks Console Ops.
    { provide: LogStreamDataSource, useClass: HttpLogStreamDataSource },
  ],
};
