import { DestroyRef, inject } from '@angular/core';

/**
 * How often a screen re-reads its stored observations.
 *
 * Comfortably shorter than the API's collection interval, so a newly recorded fact shows up soon after
 * it is recorded without the browser asking far more often than there is anything new to see.
 */
export const READ_REFRESH_INTERVAL_MS = 30_000;

/**
 * How often a screen re-reads while something it is showing is still moving.
 *
 * Short, because a run in progress is the one thing an operator watches. Paired with
 * {@link IDLE_PROVIDER_REFRESH_INTERVAL_MS} so a settled screen costs a fraction of that.
 */
export const ACTIVE_PROVIDER_REFRESH_INTERVAL_MS = 10_000;

/**
 * How often a screen re-reads once nothing is moving.
 *
 * Long, because a settled read is not free: the Workflows inventory costs a provider request per workflow, and a
 * page left open was spending that every minute to discover nothing had changed.
 */
export const IDLE_PROVIDER_REFRESH_INTERVAL_MS = 300_000;

/**
 * Re-reads a screen whose data is read from a provider during the request, on an interval it chooses.
 *
 * Separate from {@link autoRefresh} because that one re-reads observations Console Ops already recorded, which
 * costs a database query. This one costs a provider request against a shared rate limit, so the caller supplies
 * the interval and is expected to shorten it only while something is actually running - and to re-read what is
 * moving rather than everything on the page.
 *
 * A hidden tab is not polled, and returning to one re-reads immediately rather than waiting out an interval.
 *
 * Must be called from an injection context, so the timer and listener are torn down with the caller.
 */
export function providerRefresh(read: () => void, intervalMs: () => number): void {
  const destroyRef = inject(DestroyRef);

  if (typeof document === 'undefined' || typeof window === 'undefined') {
    return;
  }

  const isVisible = (): boolean => document.visibilityState !== 'hidden';
  let timer: number | undefined;
  let first = true;

  // Chained rather than a fixed interval, so a run starting shortens the next wait instead of the one after it.
  const schedule = (): void => {
    // The first wait is the short one: the page has only just asked for its data and does not yet know whether
    // anything is running, and waiting out the idle interval to find out would miss the start of a run.
    const wait = first ? ACTIVE_PROVIDER_REFRESH_INTERVAL_MS : intervalMs();
    first = false;

    timer = window.setTimeout(() => {
      if (isVisible()) {
        read();
      }

      schedule();
    }, wait);
  };

  const onVisibilityChange = (): void => {
    if (isVisible()) {
      read();
    }
  };

  schedule();
  document.addEventListener('visibilitychange', onVisibilityChange);

  destroyRef.onDestroy(() => {
    if (timer !== undefined) {
      window.clearTimeout(timer);
    }

    document.removeEventListener('visibilitychange', onVisibilityChange);
  });
}

/**
 * Re-reads a screen's data on an interval while the operator is looking at it.
 *
 * This only re-reads what the API has already recorded; it never asks the API to contact a provider.
 * Collection is the background worker's job, and the manual refresh action remains the way to say
 * "check right now".
 *
 * A hidden tab is not polled, and returning to a hidden tab reads immediately rather than waiting out
 * the remainder of an interval, so the screen is never showing stale state while being looked at.
 *
 * Must be called from an injection context, so the interval and listener are torn down with the caller.
 */
export function autoRefresh(read: () => void, intervalMs = READ_REFRESH_INTERVAL_MS): void {
  const destroyRef = inject(DestroyRef);

  if (typeof document === 'undefined' || typeof window === 'undefined') {
    return;
  }

  const isVisible = (): boolean => document.visibilityState !== 'hidden';

  const timer = window.setInterval(() => {
    if (isVisible()) {
      read();
    }
  }, intervalMs);

  const onVisibilityChange = (): void => {
    if (isVisible()) {
      read();
    }
  };

  document.addEventListener('visibilitychange', onVisibilityChange);

  destroyRef.onDestroy(() => {
    window.clearInterval(timer);
    document.removeEventListener('visibilitychange', onVisibilityChange);
  });
}
