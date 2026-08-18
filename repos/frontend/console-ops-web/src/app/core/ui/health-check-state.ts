import { StatusCell } from '../contracts/dashboard-overview';
import { HealthCheckState } from '../contracts/health';

/**
 * Wording and tone for a recorded health state, in one place.
 *
 * Health, Overview, Projects and Deployments all render these states, and the whole point of a single mapping is
 * that an environment cannot read `Unhealthy` on one screen and `Down` on another.
 *
 * `Running` is deliberately its own verdict rather than a shade of healthy: a process that answers is running,
 * but only a health endpoint can claim it is healthy. `Not configured` is not a failure, and `Unknown` means no
 * check exists - never "probably fine".
 */
export function healthCheckCell(state: HealthCheckState): StatusCell {
  switch (state) {
    case 'healthy':
      return { level: 'healthy', label: 'Healthy', detail: null };
    case 'running':
      return { level: 'running', label: 'Running', detail: null };
    case 'degraded':
      return { level: 'warning', label: 'Degraded', detail: null };
    case 'unhealthy':
      return { level: 'down', label: 'Unhealthy', detail: null };
    case 'unreachable':
      return { level: 'down', label: 'Unreachable', detail: null };
    case 'notConfigured':
      return { level: 'notApplicable', label: 'Not configured', detail: null };
    default:
      return { level: 'unknown', label: 'Unknown', detail: null };
  }
}

/** Whether a state is one an operator has to act on, which is what the Active Issues section collects. */
export function isFailingState(state: HealthCheckState): boolean {
  return state === 'degraded' || state === 'unhealthy' || state === 'unreachable';
}
