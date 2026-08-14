import { ProjectRegistry } from '../../contracts/project-registry';

/**
 * Design-stage fixture for the Projects screen.
 *
 * This is a fixture, not a fallback: only the mock adapter reaches it, so a real API failure can
 * never be masked by it. It is removed when the project list query slice lands.
 *
 * It keeps the honest gaps the product must handle:
 * - AMYL runs locally, so it has no CI-triggered deployment record;
 * - StockPilot is reachable but warning, not healthy;
 * - the archived project is no longer monitored, so its health is unknown and it has no runtime.
 */
export const PROJECT_REGISTRY_FIXTURE: ProjectRegistry = {
  observedAt: '2025-05-14T09:24:00Z',
  projects: [
    {
      id: 'spinner-api',
      name: 'Spinner API',
      description: 'ASP.NET Core Web API',
      kind: 'api',
      lifecycle: 'active',
      environment: { id: 'spinner-api-prod', name: 'Production', kind: 'production' },
      repository: {
        provider: 'github',
        repository: 'spinner/api',
        defaultBranch: 'main',
        url: 'https://github.com/spinner/api',
      },
      runtime: { framework: '.NET 8', target: 'Azure App Service' },
      health: { level: 'healthy', label: 'Healthy', detail: null },
      healthObservedAt: '2025-05-14T09:22:00Z',
      lastDeployment: { deployedAt: '2025-05-14T09:23:00Z', triggeredBy: 'ci-bot' },
      registeredAt: '2025-04-02T10:15:00Z',
    },
    {
      id: 'stalltrack',
      name: 'StallTrack',
      description: 'ASP.NET Core Web App',
      kind: 'webApp',
      lifecycle: 'active',
      environment: { id: 'stalltrack-prod', name: 'Production', kind: 'production' },
      repository: {
        provider: 'github',
        repository: 'stalltrack/web',
        defaultBranch: 'main',
        url: 'https://github.com/stalltrack/web',
      },
      runtime: { framework: '.NET 8', target: 'Azure App Service' },
      health: { level: 'healthy', label: 'Healthy', detail: null },
      healthObservedAt: '2025-05-14T08:44:00Z',
      lastDeployment: { deployedAt: '2025-05-14T08:41:00Z', triggeredBy: 'ci-bot' },
      registeredAt: '2025-04-08T09:05:00Z',
    },
    {
      id: 'amyl',
      name: 'AMYL',
      description: 'Worker Service',
      kind: 'worker',
      lifecycle: 'active',
      environment: { id: 'amyl-local', name: 'Local', kind: 'local' },
      repository: {
        provider: 'github',
        repository: 'amyl/worker',
        defaultBranch: 'develop',
        url: 'https://github.com/amyl/worker',
      },
      runtime: { framework: '.NET 8', target: 'Docker Desktop' },
      health: { level: 'running', label: 'Running', detail: 'Docker active' },
      healthObservedAt: '2025-05-14T09:20:00Z',
      lastDeployment: { deployedAt: '2025-05-14T07:48:00Z', triggeredBy: 'developer' },
      registeredAt: '2025-05-12T14:40:00Z',
    },
    {
      id: 'console-ops',
      name: 'Console Ops',
      description: 'Internal Dashboard',
      kind: 'webApp',
      lifecycle: 'active',
      environment: { id: 'console-ops-prod', name: 'Production', kind: 'production' },
      repository: {
        provider: 'github',
        repository: 'console/ops',
        defaultBranch: 'release/1.2',
        url: 'https://github.com/console/ops',
      },
      runtime: { framework: '.NET 8', target: 'Azure App Service' },
      health: { level: 'healthy', label: 'Healthy', detail: null },
      healthObservedAt: '2025-05-14T09:21:00Z',
      lastDeployment: { deployedAt: '2025-05-13T17:32:00Z', triggeredBy: 'ci-bot' },
      registeredAt: '2025-05-13T16:20:00Z',
    },
    {
      id: 'stockpilot',
      name: 'StockPilot',
      description: 'Background Service',
      kind: 'worker',
      lifecycle: 'active',
      environment: { id: 'stockpilot-prod', name: 'Production', kind: 'production' },
      repository: {
        provider: 'github',
        repository: 'stockpilot/worker',
        defaultBranch: 'main',
        url: 'https://github.com/stockpilot/worker',
      },
      runtime: { framework: '.NET 8', target: 'Azure Container Apps' },
      health: { level: 'warning', label: 'Warning', detail: 'Elevated queue depth' },
      healthObservedAt: '2025-05-14T09:18:00Z',
      lastDeployment: { deployedAt: '2025-05-13T11:09:00Z', triggeredBy: 'ci-bot' },
      registeredAt: '2025-05-13T10:55:00Z',
    },
    {
      id: 'legacy-billing',
      name: 'Legacy Billing',
      description: 'ASP.NET Core Web API',
      kind: 'api',
      lifecycle: 'archived',
      environment: { id: 'legacy-billing-prod', name: 'Production', kind: 'production' },
      repository: {
        provider: 'github',
        repository: 'legacy/billing',
        defaultBranch: 'main',
        url: 'https://github.com/legacy/billing',
      },
      runtime: null,
      health: { level: 'unknown', label: 'Unknown', detail: 'Not monitored while archived' },
      healthObservedAt: null,
      lastDeployment: null,
      registeredAt: '2024-11-18T08:30:00Z',
    },
  ],
};
