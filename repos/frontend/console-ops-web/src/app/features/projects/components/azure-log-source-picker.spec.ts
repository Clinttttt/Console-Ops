import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { AzureLogSource, AzureLogSources } from '../../../core/contracts/azure-discovery';
import {
  AzureDiscoveryDataSource,
  HttpAzureDiscoveryDataSource,
} from '../../../core/data/azure-discovery.data-source';
import { AzureLogSourcePicker } from './azure-log-source-picker';

const WORKSPACE = '6f5c1a2b-3d4e-5f60-7182-93a4b5c6d7e8';

function app(overrides: Partial<AzureLogSource>): AzureLogSource {
  return {
    provider: 'azureContainerApps',
    containerAppName: 'spinner-api',
    resourceGroup: 'spinner-rg',
    subscriptionId: '11111111-2222-3333-4444-555555555555',
    location: 'southeastasia',
    environmentName: 'spinner-env',
    workspaceId: WORKSPACE,
    ...overrides,
  };
}

const SOURCES: AzureLogSources = {
  containerApps: [
    app({}),
    app({ containerAppName: 'no-logs-app', environmentName: 'bare-env', workspaceId: null }),
  ],
  hasMore: false,
};

describe('AzureLogSourcePicker', () => {
  let fixture: ComponentFixture<AzureLogSourcePicker>;
  let host: HTMLElement;
  let calls: number;

  async function render(
    listLogSources: () => ReturnType<AzureDiscoveryDataSource['listLogSources']>,
  ): Promise<void> {
    calls = 0;
    await TestBed.configureTestingModule({
      imports: [AzureLogSourcePicker],
      providers: [
        {
          provide: AzureDiscoveryDataSource,
          useValue: {
            listLogSources: () => {
              calls += 1;
              return listLogSources();
            },
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AzureLogSourcePicker);
    await fixture.whenStable();
    host = fixture.nativeElement as HTMLElement;
  }

  function trigger(): HTMLButtonElement {
    return host.querySelector<HTMLButtonElement>('.trigger')!;
  }

  async function open(): Promise<void> {
    trigger().click();
    await fixture.whenStable();
  }

  it('asks Azure only when the picker is opened', async () => {
    await render(() => of(SOURCES));

    expect(calls).toBe(0);
    expect(host.querySelector('.panel')).toBeNull();

    await open();

    expect(calls).toBe(1);
    expect(host.querySelectorAll('.app').length).toBe(2);
  });

  it('offers an app whose environment has a workspace, and marks one that has none', async () => {
    await render(() => of(SOURCES));
    await open();

    const [offered, unavailable] = Array.from(host.querySelectorAll('.app'));
    expect(offered.tagName).toBe('BUTTON');
    expect(offered.textContent).toContain('spinner-api');
    expect(offered.textContent).toContain('spinner-env');
    // No workspace means Console Ops could not read it, so it is not selectable.
    expect(unavailable.tagName).toBe('SPAN');
    expect(unavailable.textContent).toContain('No log workspace');
  });

  it('emits the chosen app and closes, so both fields can be filled from one pick', async () => {
    await render(() => of(SOURCES));
    await open();

    const chosen: AzureLogSource[] = [];
    fixture.componentInstance.choose.subscribe((source) => chosen.push(source));
    host.querySelector<HTMLButtonElement>('button.app')!.click();
    await fixture.whenStable();

    expect(chosen.length).toBe(1);
    expect(chosen[0].containerAppName).toBe('spinner-api');
    expect(chosen[0].workspaceId).toBe(WORKSPACE);
    expect(host.querySelector('.panel')).toBeNull();
  });

  it('names a rejected Azure identity rather than blaming the network', async () => {
    await render(() =>
      throwError(
        () =>
          new HttpErrorResponse({
            status: 500,
            error: { code: 'Azure.Unauthorized' },
          }),
      ),
    );
    await open();

    expect(host.textContent).toContain('Azure rejected the identity');
    expect(host.textContent).toContain('az login');
  });

  it('says the API could not be reached when Console Ops itself is down', async () => {
    await render(() => throwError(() => new HttpErrorResponse({ status: 0 })));
    await open();

    expect(host.textContent).toContain('Console Ops API is unavailable');
  });

  it('says a visible-but-empty tenant is empty, without implying a failure', async () => {
    await render(() => of({ containerApps: [], hasMore: false }));
    await open();

    expect(host.textContent).toContain('No container apps are visible');
  });

  it('says the list is not everything when Azure had more', async () => {
    await render(() => of({ containerApps: [app({})], hasMore: true }));
    await open();

    expect(host.textContent).toContain('More apps exist than are listed');
  });
});

describe('HttpAzureDiscoveryDataSource', () => {
  it('reads container apps from the Console Ops endpoint, never from Azure directly', () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), HttpAzureDiscoveryDataSource],
    });
    const dataSource = TestBed.inject(HttpAzureDiscoveryDataSource);
    const http = TestBed.inject(HttpTestingController);

    dataSource.listLogSources('spin').subscribe();
    const request = http.expectOne('/api/azure/log-sources?query=spin');
    expect(request.request.method).toBe('GET');
    request.flush(SOURCES);

    dataSource.listLogSources('  ').subscribe();
    http.expectOne('/api/azure/log-sources').flush(SOURCES);
    http.verify();
  });
});
