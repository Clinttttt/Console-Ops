import { StatusCell } from '../contracts/dashboard-overview';
import { HealthState, VersionSyncState } from '../contracts/deployment-registry';

/**
 * Wording for a recorded health state.
 *
 * Kept identical to the wording the dashboard query emits, so the same observation never reads as
 * `Unhealthy` on one screen and `Down` on another.
 */
export function healthStateCell(state: HealthState): StatusCell {
  switch (state) {
    case 'healthy':
      return { level: 'healthy', label: 'Healthy', detail: null };
    case 'degraded':
      return { level: 'degraded', label: 'Degraded', detail: null };
    case 'unhealthy':
      return { level: 'down', label: 'Unhealthy', detail: null };
    case 'unreachable':
      return { level: 'down', label: 'Unreachable', detail: null };
    case 'notConfigured':
      return { level: 'unknown', label: 'Not configured', detail: null };
    default:
      return { level: 'unknown', label: 'Not observed', detail: null };
  }
}

/** Wording for a recorded version-sync state, matching the Overview and Environments screens. */
export function versionSyncCell(state: VersionSyncState): StatusCell {
  switch (state) {
    case 'inSync':
      return { level: 'healthy', label: 'In Sync', detail: null };
    case 'behind':
      return { level: 'warning', label: 'Behind', detail: null };
    case 'notConfigured':
      return { level: 'unknown', label: 'Not configured', detail: null };
    default:
      return { level: 'unknown', label: 'Unknown', detail: null };
  }
}
