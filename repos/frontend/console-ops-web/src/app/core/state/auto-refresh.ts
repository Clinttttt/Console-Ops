import { DestroyRef, inject } from '@angular/core';

/**
 * How often a screen re-reads its stored observations.
 *
 * Comfortably shorter than the API's collection interval, so a newly recorded fact shows up soon after
 * it is recorded without the browser asking far more often than there is anything new to see.
 */
export const READ_REFRESH_INTERVAL_MS = 30_000;

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
