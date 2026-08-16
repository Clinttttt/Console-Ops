import { TestBed } from '@angular/core/testing';
import { Observable, Subject, throwError } from 'rxjs';

import { DeploymentRegistry } from '../contracts/deployment-registry';
import { DeploymentRegistryDataSource } from '../data/deployment-registry.data-source';
import { DeploymentRegistryStore } from './deployment-registry.store';

function registry(observedAt: string, commitShortSha: string): DeploymentRegistry {
  return {
    observedAt,
    deployments: [
      {
        id: `deployment-${commitShortSha}`,
        projectId: 'project-1',
        projectName: 'Spinner API',
        provider: 'githubActions',
        repository: 'spinner/api',
        branch: 'main',
        commitSha: `${commitShortSha}9abcdef0123456789abcdef0123456789`.slice(0, 40),
        commitShortSha,
        result: 'passed',
        workflowFile: 'deploy.yml',
        workflowName: 'Deploy',
        workflowUrl: null,
        runNumber: 1,
        triggeredBy: 'ci-bot',
        startedAt: null,
        completedAt: null,
        deployedAt: observedAt,
        durationSeconds: null,
        recordedAt: observedAt,
        environments: [],
      },
    ],
  };
}

/** A data source the test drives, so the state between a request and its response can be inspected. */
class ControlledDataSource extends DeploymentRegistryDataSource {
  readonly responses: Subject<DeploymentRegistry>[] = [];
  failNext = false;

  override load(): Observable<DeploymentRegistry> {
    if (this.failNext) {
      return throwError(() => new Error('unreachable'));
    }

    const response = new Subject<DeploymentRegistry>();
    this.responses.push(response);
    return response;
  }

  respond(registry: DeploymentRegistry): void {
    const response = this.responses.at(-1);
    response?.next(registry);
    response?.complete();
  }
}

describe('DeploymentRegistryStore', () => {
  let dataSource: ControlledDataSource;
  let store: DeploymentRegistryStore;

  beforeEach(() => {
    dataSource = new ControlledDataSource();
    TestBed.configureTestingModule({
      providers: [{ provide: DeploymentRegistryDataSource, useValue: dataSource }],
    });
    store = TestBed.inject(DeploymentRegistryStore);
  });

  it('reports loading only until the first read arrives', () => {
    expect(store.loadState()).toBe('loading');

    dataSource.respond(registry('2026-08-14T09:00:00Z', '8a17c2f'));

    expect(store.loadState()).toBe('loaded');
    expect(store.deployments().length).toBe(1);
  });

  it('keeps showing the timeline while a scheduled re-read is in flight', () => {
    dataSource.respond(registry('2026-08-14T09:00:00Z', '8a17c2f'));

    store.refresh();

    // The screen must not fall back to a loading notice while it is being looked at.
    expect(store.loadState()).toBe('loaded');
    expect(store.deployments()[0].commitShortSha).toBe('8a17c2f');

    dataSource.respond(registry('2026-08-14T09:30:00Z', '71be129'));

    expect(store.deployments()[0].commitShortSha).toBe('71be129');
    expect(store.observedAt()).toBe('2026-08-14T09:30:00Z');
  });

  it('keeps the last known history when a re-read fails', () => {
    dataSource.respond(registry('2026-08-14T09:00:00Z', '8a17c2f'));
    dataSource.failNext = true;

    store.refresh();

    // The previous reading is still the most recent thing Console Ops knows.
    expect(store.loadState()).toBe('loaded');
    expect(store.deployments()[0].commitShortSha).toBe('8a17c2f');
  });

  it('reports unavailable when the very first read fails', () => {
    TestBed.resetTestingModule();
    const failing = new ControlledDataSource();
    failing.failNext = true;
    TestBed.configureTestingModule({
      providers: [{ provide: DeploymentRegistryDataSource, useValue: failing }],
    });

    const failedStore = TestBed.inject(DeploymentRegistryStore);

    expect(failedStore.loadState()).toBe('unavailable');
    expect(failedStore.deployments()).toEqual([]);
  });
});
