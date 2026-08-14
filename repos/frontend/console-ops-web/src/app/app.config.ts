import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withInMemoryScrolling } from '@angular/router';

import { routes } from './app.routes';
import { DashboardOverviewDataSource } from './core/data/dashboard-overview.data-source';
import { MockDashboardOverviewDataSource } from './core/data/mock/mock-dashboard-overview.data-source';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withInMemoryScrolling({ scrollPositionRestoration: 'top' })),

    // Single swap point for real data: replace this provider with the HTTP adapter for
    // GET /api/dashboard/overview (and add provideHttpClient) once the backend slice exists.
    // No component or template changes are required.
    { provide: DashboardOverviewDataSource, useClass: MockDashboardOverviewDataSource },
  ],
};
