import { TestBed } from '@angular/core/testing';
import { Injector, runInInjectionContext } from '@angular/core';

import {
  ACTIVE_PROVIDER_REFRESH_INTERVAL_MS,
  IDLE_PROVIDER_REFRESH_INTERVAL_MS,
  providerRefresh,
} from './auto-refresh';

/**
 * The polling behaviour of a screen whose data is read from a provider during the request.
 *
 * What matters is the cost: it must follow a run closely while one is going and go quiet when nothing is, and it
 * must not poll a tab nobody is looking at.
 */
describe('providerRefresh', () => {
  let injector: Injector;

  beforeEach(() => {
    TestBed.resetTestingModule();
    vi.useFakeTimers();
    injector = TestBed.inject(Injector);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  function start(read: () => void, running: () => boolean): void {
    runInInjectionContext(injector, () =>
      providerRefresh(read, () =>
        running() ? ACTIVE_PROVIDER_REFRESH_INTERVAL_MS : IDLE_PROVIDER_REFRESH_INTERVAL_MS,
      ),
    );
  }

  it('reads soon after starting, before it can know whether anything is running', () => {
    let reads = 0;
    start(
      () => reads++,
      () => false,
    );

    // Waiting out the idle interval to discover a run had started would miss the start of it.
    vi.advanceTimersByTime(ACTIVE_PROVIDER_REFRESH_INTERVAL_MS);
    expect(reads).toBe(1);
  });

  it('follows a running run closely and goes quiet once it finishes', () => {
    let reads = 0;
    let running = true;
    start(
      () => reads++,
      () => running,
    );

    vi.advanceTimersByTime(ACTIVE_PROVIDER_REFRESH_INTERVAL_MS * 3);
    expect(reads).toBe(3);

    running = false;
    // The tick already scheduled still fires; it is the wait after it that lengthens.
    vi.advanceTimersByTime(ACTIVE_PROVIDER_REFRESH_INTERVAL_MS);
    expect(reads).toBe(4);

    // From here a settled screen stops asking the provider every few seconds.
    vi.advanceTimersByTime(ACTIVE_PROVIDER_REFRESH_INTERVAL_MS * 3);
    expect(reads).toBe(4);

    vi.advanceTimersByTime(IDLE_PROVIDER_REFRESH_INTERVAL_MS);
    expect(reads).toBe(5);
  });

  it('does not read while the tab is hidden, and reads as soon as it is looked at again', () => {
    let reads = 0;
    const visibility = vi.spyOn(document, 'visibilityState', 'get');
    visibility.mockReturnValue('hidden');
    start(
      () => reads++,
      () => true,
    );

    vi.advanceTimersByTime(ACTIVE_PROVIDER_REFRESH_INTERVAL_MS * 4);
    expect(reads).toBe(0);

    visibility.mockReturnValue('visible');
    document.dispatchEvent(new Event('visibilitychange'));
    expect(reads).toBe(1);

    visibility.mockRestore();
  });
});
