import { ProjectRegistry } from '../../contracts/project-registry';

/** Representative V1 project resources used only by component and HTTP adapter tests. */
export const PROJECT_REGISTRY_FIXTURE: ProjectRegistry = [
  {
    id: 'spinner-api',
    name: 'Spinner API',
    description: 'ASP.NET Core Web API',
    repository: {
      owner: 'spinner',
      name: 'api',
      defaultBranch: 'main',
      workflowFile: 'deploy.yml',
    },
    environments: [
      {
        id: 'spinner-api-prod',
        name: 'Production',
        kind: 'production',
        applicationUrl: 'https://api.spinner.example',
        healthUrl: 'https://api.spinner.example/health',
        versionUrl: 'https://api.spinner.example/version',
      },
    ],
    createdAtUtc: '2025-04-02T10:15:00Z',
    updatedAtUtc: '2025-05-14T09:23:00Z',
    configurationVersion: 2,
  },
  {
    id: 'stalltrack',
    name: 'StallTrack',
    description: 'ASP.NET Core Web App',
    repository: {
      owner: 'stalltrack',
      name: 'web',
      defaultBranch: 'main',
      workflowFile: null,
    },
    environments: [
      {
        id: 'stalltrack-prod',
        name: 'Production',
        kind: 'production',
        applicationUrl: 'https://stalltrack.example',
        healthUrl: 'https://stalltrack.example/health',
        versionUrl: null,
      },
    ],
    createdAtUtc: '2025-04-08T09:05:00Z',
    updatedAtUtc: null,
    configurationVersion: 1,
  },
  {
    id: 'amyl',
    name: 'AMYL',
    description: 'Worker Service',
    repository: {
      owner: 'amyl',
      name: 'worker',
      defaultBranch: 'develop',
      workflowFile: null,
    },
    environments: [
      {
        id: 'amyl-local',
        name: 'Local',
        kind: 'local',
        applicationUrl: null,
        healthUrl: null,
        versionUrl: null,
      },
    ],
    createdAtUtc: '2025-05-12T14:40:00Z',
    updatedAtUtc: null,
    configurationVersion: 1,
  },
  {
    id: 'console-ops',
    name: 'Console Ops',
    description: 'Developer operations control center',
    repository: {
      owner: 'console',
      name: 'ops',
      defaultBranch: 'main',
      workflowFile: null,
    },
    environments: [
      {
        id: 'console-ops-dev',
        name: 'Development',
        kind: 'development',
        applicationUrl: 'http://localhost:5096',
        healthUrl: null,
        versionUrl: null,
      },
    ],
    createdAtUtc: '2025-05-13T16:20:00Z',
    updatedAtUtc: null,
    configurationVersion: 1,
  },
  {
    id: 'stockpilot',
    name: 'StockPilot',
    description: 'Background Service',
    repository: {
      owner: 'stockpilot',
      name: 'worker',
      defaultBranch: 'main',
      workflowFile: 'ci.yml',
    },
    environments: [
      {
        id: 'stockpilot-prod',
        name: 'Production',
        kind: 'production',
        applicationUrl: null,
        healthUrl: null,
        versionUrl: null,
      },
      {
        id: 'stockpilot-local',
        name: 'Local',
        kind: 'local',
        applicationUrl: null,
        healthUrl: null,
        versionUrl: null,
      },
    ],
    createdAtUtc: '2025-05-13T10:55:00Z',
    updatedAtUtc: null,
    configurationVersion: 1,
  },
];
