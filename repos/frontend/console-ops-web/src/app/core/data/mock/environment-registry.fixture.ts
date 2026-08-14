import { EnvironmentRegistry } from '../../contracts/environment-registry';

/**
 * Design-stage fixture for the Environments screen.
 *
 * Only the mock adapter reaches it, so a real API failure can never be masked by it. Several columns
 * on this screen depend on later product phases (runtime target, Azure revision, configuration
 * presence, deployment time), so the screen stays fixture-backed until those phases exist; see
 * "Environments screen: design mock ahead of later-phase data" in the architecture document.
 *
 * Instants are relative to `observedAt` so the screen's relative times are stable and testable.
 * Honest gaps kept deliberately:
 * - AMYL is local with no version endpoint, so version sync is `notConfigured` and it has no
 *   revision, no deployment record, and no configuration check;
 * - Console Ops staging is behind source and missing one expected configuration key;
 * - the archived environment is no longer monitored, so its health is unknown.
 */
export const ENVIRONMENT_REGISTRY_FIXTURE: EnvironmentRegistry = {
  observedAt: '2025-05-14T09:24:00Z',
  environments: [
    {
      id: 'spinner-api-prod',
      projectId: 'spinner-api',
      projectName: 'Spinner API',
      name: 'Production',
      kind: 'production',
      lifecycle: 'active',
      runtime: { provider: 'azure', target: 'Azure Container Apps' },
      applicationUrl: 'https://api.spinnerapp.com',
      healthEndpoint: '/health',
      versionEndpoint: '/version',
      health: { level: 'healthy', label: 'Healthy', detail: null },
      healthCheckedAt: '2025-05-14T09:23:46Z',
      versionSync: { state: 'inSync', deployedCommitShortSha: '8a17c2f' },
      currentRevision: 'spinner-api--000021',
      configuration: { expectedKeys: 5, configuredKeys: 5 },
      lastDeployedAt: '2025-05-14T09:06:00Z',
    },
    {
      id: 'stalltrack-prod',
      projectId: 'stalltrack',
      projectName: 'StallTrack',
      name: 'Production',
      kind: 'production',
      lifecycle: 'active',
      runtime: { provider: 'azure', target: 'Azure Container Apps' },
      applicationUrl: 'https://api.stalltrack.site',
      healthEndpoint: '/health',
      versionEndpoint: '/version',
      health: { level: 'healthy', label: 'Healthy', detail: null },
      healthCheckedAt: '2025-05-14T09:22:10Z',
      versionSync: { state: 'inSync', deployedCommitShortSha: '4f91b9a' },
      currentRevision: 'stalltrack--000018',
      configuration: { expectedKeys: 6, configuredKeys: 6 },
      lastDeployedAt: '2025-05-14T07:24:00Z',
    },
    {
      id: 'amyl-local',
      projectId: 'amyl',
      projectName: 'AMYL',
      name: 'Local',
      kind: 'local',
      lifecycle: 'active',
      runtime: { provider: 'docker', target: 'Docker Desktop' },
      applicationUrl: 'http://localhost:7027',
      healthEndpoint: '/health',
      versionEndpoint: null,
      health: { level: 'running', label: 'Running', detail: 'Docker active' },
      healthCheckedAt: '2025-05-14T09:20:00Z',
      versionSync: { state: 'notConfigured', deployedCommitShortSha: null },
      currentRevision: null,
      configuration: null,
      lastDeployedAt: null,
    },
    {
      id: 'console-ops-staging',
      projectId: 'console-ops',
      projectName: 'Console Ops',
      name: 'Staging',
      kind: 'staging',
      lifecycle: 'active',
      runtime: { provider: 'azure', target: 'Azure Container Apps' },
      applicationUrl: 'https://staging.consoleops.app',
      healthEndpoint: '/health',
      versionEndpoint: '/version',
      health: { level: 'healthy', label: 'Healthy', detail: null },
      healthCheckedAt: '2025-05-14T09:21:30Z',
      versionSync: { state: 'behind', deployedCommitShortSha: '1c4de90' },
      currentRevision: 'console-ops--000007',
      configuration: { expectedKeys: 5, configuredKeys: 4 },
      lastDeployedAt: '2025-05-13T09:24:00Z',
    },
    {
      id: 'stockpilot-prod',
      projectId: 'stockpilot',
      projectName: 'StockPilot',
      name: 'Production',
      kind: 'production',
      lifecycle: 'active',
      runtime: { provider: 'azure', target: 'Azure Container Apps' },
      applicationUrl: 'https://api.stockpilot.app',
      healthEndpoint: '/health',
      versionEndpoint: '/version',
      health: { level: 'warning', label: 'Warning', detail: 'Elevated queue depth' },
      healthCheckedAt: '2025-05-14T09:18:00Z',
      versionSync: { state: 'inSync', deployedCommitShortSha: '7b2ac41' },
      currentRevision: 'stockpilot--000012',
      configuration: { expectedKeys: 7, configuredKeys: 7 },
      lastDeployedAt: '2025-05-14T04:24:00Z',
    },
    {
      id: 'legacy-billing-prod',
      projectId: 'legacy-billing',
      projectName: 'Legacy Billing',
      name: 'Production',
      kind: 'production',
      lifecycle: 'archived',
      runtime: null,
      applicationUrl: null,
      healthEndpoint: null,
      versionEndpoint: null,
      health: { level: 'unknown', label: 'Unknown', detail: 'Not monitored while archived' },
      healthCheckedAt: null,
      versionSync: { state: 'notConfigured', deployedCommitShortSha: null },
      currentRevision: null,
      configuration: null,
      lastDeployedAt: null,
    },
  ],
};
