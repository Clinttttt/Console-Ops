import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter, withInMemoryScrolling } from '@angular/router';

import { routes } from './app.routes';
import { DashboardOverviewDataSource } from './core/data/dashboard-overview.data-source';
import { HttpDashboardOverviewDataSource } from './core/data/http-dashboard-overview.data-source';
import { MockProjectRegistryDataSource } from './core/data/mock/mock-project-registry.data-source';
import { ProjectRegistryDataSource } from './core/data/project-registry.data-source';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(),
    provideRouter(routes, withInMemoryScrolling({ scrollPositionRestoration: 'top' })),
    { provide: DashboardOverviewDataSource, useClass: HttpDashboardOverviewDataSource },

    // Design stage: the Projects screen runs on a fixture because the project list read model in
    // `core/contracts/project-registry.ts` still exceeds the frozen V1 project resource. Replace
    // this one provider with an HTTP adapter once that list query exists, then delete the mock.
    { provide: ProjectRegistryDataSource, useClass: MockProjectRegistryDataSource },
  ],
};
