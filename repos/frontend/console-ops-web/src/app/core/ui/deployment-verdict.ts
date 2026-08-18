import { DeploymentListItem, HealthState } from '../contracts/deployment-registry';
import { StatusCell } from '../contracts/dashboard-overview';

/**
 * Deterministic verification verdict for one release.
 *
 * Derived only from facts the provider and the probes reported: how the run ended, the health observed
 * after the release was first seen running, and version sync. Nothing is inferred. A release that was
 * never seen running reads as unobserved rather than failed, and an unknown fact yields `Unverified`
 * rather than a pass.
 *
 * Drift is only reported for an environment the release is still serving. A superseded release is
 * expected to be behind, so saying so would be noise rather than information.
 */
export function deploymentVerdict(deployment: DeploymentListItem): StatusCell {
  if (deployment.result === 'failed') {
    return { level: 'down', label: 'Failed', detail: 'Workflow run failed' };
  }

  if (deployment.result === 'cancelled') {
    return { level: 'unknown', label: 'Cancelled', detail: null };
  }

  if (deployment.result === 'inProgress') {
    return { level: 'running', label: 'In progress', detail: null };
  }

  if (deployment.result === 'queued') {
    return { level: 'running', label: 'Queued', detail: null };
  }

  if (deployment.result === 'unknown') {
    return { level: 'unknown', label: 'Unknown', detail: 'Run outcome not reported' };
  }

  const environments = deployment.environments;
  if (environments.length === 0) {
    // The run passed. What is missing is evidence of it running, which is a weaker claim than failure
    // and a different one from "nothing happened".
    return {
      level: 'unknown',
      label: 'Unverified',
      detail: 'Passed, but never seen running in an environment',
    };
  }

  if (environments.some((environment) => isFailing(environment.healthAfter))) {
    return { level: 'down', label: 'Failed', detail: 'Health failed after the release' };
  }

  if (environments.some((environment) => environment.healthAfter === 'degraded')) {
    return { level: 'degraded', label: 'Degraded', detail: 'Degraded after the release' };
  }

  if (
    environments.some(
      (environment) => environment.isCurrent && environment.versionCheck === 'behind',
    )
  ) {
    return { level: 'warning', label: 'Behind', detail: 'Source has moved on' };
  }

  if (environments.some((environment) => environment.healthAfter === 'unknown')) {
    return { level: 'unknown', label: 'Unverified', detail: 'No health check after the release' };
  }

  return { level: 'healthy', label: 'Passed', detail: null };
}

/** `true` when a release is still the one serving at least one environment. */
export function isCurrentRelease(deployment: DeploymentListItem): boolean {
  return deployment.environments.some((environment) => environment.isCurrent);
}

function isFailing(state: HealthState): boolean {
  return state === 'unhealthy' || state === 'unreachable';
}
