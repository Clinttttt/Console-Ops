import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { DASHBOARD_OVERVIEW_FIXTURE } from './mock/dashboard-overview.fixture';
import { HttpDashboardOverviewDataSource } from './http-dashboard-overview.data-source';

describe('HttpDashboardOverviewDataSource', () => {
  it('reads the dashboard overview from the backend contract endpoint', () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), HttpDashboardOverviewDataSource],
    });
    const dataSource = TestBed.inject(HttpDashboardOverviewDataSource);
    const http = TestBed.inject(HttpTestingController);
    let actual = null;

    dataSource.load().subscribe((overview) => (actual = overview));
    const request = http.expectOne('/api/dashboard/overview');
    expect(request.request.method).toBe('GET');
    request.flush(DASHBOARD_OVERVIEW_FIXTURE);

    expect(actual).toEqual(DASHBOARD_OVERVIEW_FIXTURE);
    http.verify();
  });
});
