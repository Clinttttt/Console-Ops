import { DeploymentListItem } from '../contracts/deployment-registry';
import { StatusCell } from '../contracts/dashboard-overview';

/**
 * Deterministic verification verdict for one deployment.
 *
 * Derived only from explicit facts the provider and the probes reported - result, post-deployment
 * health, and version sync - in that order of severity. Nothing is inferred beyond those facts: a
 * missing version endpoint yields `notConfigured`, which never downgrades an otherwise passing
 * deployment, and an unknown fact yields `Unknown` rather than a guess.
 */
export function deploymentVerdict(deployment: DeploymentListItem): StatusCell {
  if (deployment.result === 'failed' || deployment.healthCheck === 'failed') {
    return { level: 'down', label: 'Failed', detail: failureDetail(deployment) };
  }

  if (deployment.result === 'cancelled') {
    return { level: 'unknown', label: 'Cancelled', detail: null };
  }

  if (deployment.result === 'inProgress') {
    return { level: 'running', label: 'In progress', detail: null };
  }

  if (deployment.result === 'unknown') {
    return { level: 'unknown', label: 'Unknown', detail: 'Deployment result not reported' };
  }

  if (deployment.versionCheck === 'behind') {
    return { level: 'warning', label: 'Behind', detail: 'Source has moved on' };
  }

  if (deployment.healthCheck === 'unknown' || deployment.versionCheck === 'unknown') {
    return { level: 'unknown', label: 'Unverified', detail: 'Post-deployment checks incomplete' };
  }

  return { level: 'healthy', label: 'Passed', detail: null };
}

function failureDetail(deployment: DeploymentListItem): string | null {
  if (deployment.result === 'failed') {
    return 'Deployment reported failure';
  }
  return deployment.healthCheck === 'failed' ? 'Health check failed after deployment' : null;
}
