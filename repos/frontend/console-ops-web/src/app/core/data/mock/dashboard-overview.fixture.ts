import { DashboardOverview } from '../../contracts/dashboard-overview';

/**
 * Test fixture for the V1 Overview contract. It is never registered as a runtime fallback.
 *
 * It demonstrates real V1 capabilities plus honest gaps. It deliberately does not claim Azure
 * revisions, deployments, restarts, migrations, Docker state, configuration completeness, logs, or
 * uptime history.
 */
export const DASHBOARD_OVERVIEW_FIXTURE: DashboardOverview = {
  observedAt: '2026-08-13T18:24:00Z',
  pipeline: {
    stages: [
      { key: 'source', name: 'GitHub', role: 'Source', verified: true },
      { key: 'ci', name: 'GitHub Actions', role: 'CI/CD', verified: false },
      {
        key: 'application',
        name: 'Application',
        role: 'Health & version',
        verified: false,
      },
    ],
    outcome: {
      level: 'warning',
      label: 'Partial visibility',
      detail: 'Some signals not configured',
    },
  },
  projects: [
    {
      id: 'spinner-api',
      name: 'Spinner API',
      environment: {
        id: 'spinner-api-production',
        name: 'Production',
        kind: 'production',
      },
      source: {
        provider: 'github',
        repository: 'clint/spinner',
        branch: 'main',
        commitSha: '8a17c2f4e1b9d0a6c3f5e2b8d7a4c1f0e9b6d3a2',
        commitShortSha: '8a17c2f',
        committedAt: '2026-08-13T18:18:00Z',
        observedAt: '2026-08-13T18:24:00Z',
      },
      workflow: {
        provider: 'githubActions',
        workflowName: 'deploy-production.yml',
        state: 'passed',
        commitSha: '8a17c2f4e1b9d0a6c3f5e2b8d7a4c1f0e9b6d3a2',
        startedAt: '2026-08-13T18:19:00Z',
        completedAt: '2026-08-13T18:22:00Z',
        observedAt: '2026-08-13T18:24:00Z',
      },
      health: { level: 'healthy', label: 'Healthy', detail: null },
      healthObservedAt: '2026-08-13T18:24:00Z',
      versionState: 'available',
      deployedVersion: {
        application: 'Spinner.Api',
        version: '1.4.2',
        commitSha: '8a17c2f4e1b9d0a6c3f5e2b8d7a4c1f0e9b6d3a2',
        commitShortSha: '8a17c2f',
        environment: 'Production',
        builtAt: '2026-08-13T18:20:00Z',
        observedAt: '2026-08-13T18:24:00Z',
      },
      versionSync: {
        state: 'inSync',
        sourceCommitSha: '8a17c2f4e1b9d0a6c3f5e2b8d7a4c1f0e9b6d3a2',
        deployedCommitSha: '8a17c2f4e1b9d0a6c3f5e2b8d7a4c1f0e9b6d3a2',
        commitsBehind: null,
        observedAt: '2026-08-13T18:24:00Z',
      },
      response: {
        milliseconds: 103,
        samples: [118, 109, 112, 101, 106, 98, 104, 103],
        observedAt: '2026-08-13T18:24:00Z',
      },
    },
    {
      id: 'stalltrack',
      name: 'StallTrack',
      environment: {
        id: 'stalltrack-production',
        name: 'Production',
        kind: 'production',
      },
      source: {
        provider: 'github',
        repository: 'clint/stalltrack',
        branch: 'main',
        commitSha: '4f91b9a2c8e5d3b7a1f6c4e9d2b8a5c3f7e1d9b4',
        commitShortSha: '4f91b9a',
        committedAt: '2026-08-13T17:38:00Z',
        observedAt: '2026-08-13T18:24:00Z',
      },
      workflow: {
        provider: 'githubActions',
        workflowName: 'deploy-production.yml',
        state: 'passed',
        commitSha: '4f91b9a2c8e5d3b7a1f6c4e9d2b8a5c3f7e1d9b4',
        startedAt: '2026-08-13T17:39:00Z',
        completedAt: '2026-08-13T17:42:00Z',
        observedAt: '2026-08-13T18:24:00Z',
      },
      health: { level: 'healthy', label: 'Healthy', detail: null },
      healthObservedAt: '2026-08-13T18:24:00Z',
      versionState: 'available',
      deployedVersion: {
        application: 'StallTrack',
        version: '2.1.0',
        commitSha: '4f91b9a2c8e5d3b7a1f6c4e9d2b8a5c3f7e1d9b4',
        commitShortSha: '4f91b9a',
        environment: 'Production',
        builtAt: '2026-08-13T17:40:00Z',
        observedAt: '2026-08-13T18:24:00Z',
      },
      versionSync: {
        state: 'inSync',
        sourceCommitSha: '4f91b9a2c8e5d3b7a1f6c4e9d2b8a5c3f7e1d9b4',
        deployedCommitSha: '4f91b9a2c8e5d3b7a1f6c4e9d2b8a5c3f7e1d9b4',
        commitsBehind: null,
        observedAt: '2026-08-13T18:24:00Z',
      },
      response: {
        milliseconds: 91,
        samples: [96, 103, 88, 94, 90, 97, 89, 91],
        observedAt: '2026-08-13T18:24:00Z',
      },
    },
    {
      id: 'amyl',
      name: 'AMYL',
      environment: { id: 'amyl-local', name: 'Local', kind: 'local' },
      source: {
        provider: 'github',
        repository: 'clint/amyl',
        branch: 'develop',
        commitSha: '19cf281a59b324407b0e7f9b736d812895c62912',
        commitShortSha: '19cf281',
        committedAt: '2026-08-13T16:54:00Z',
        observedAt: '2026-08-13T18:24:00Z',
      },
      workflow: {
        provider: 'githubActions',
        workflowName: null,
        state: 'notConfigured',
        commitSha: null,
        startedAt: null,
        completedAt: null,
        observedAt: null,
      },
      health: { level: 'unknown', label: 'Not configured', detail: 'No health endpoint' },
      healthObservedAt: null,
      versionState: 'notConfigured',
      deployedVersion: null,
      versionSync: {
        state: 'notConfigured',
        sourceCommitSha: '19cf281a59b324407b0e7f9b736d812895c62912',
        deployedCommitSha: null,
        commitsBehind: null,
        observedAt: null,
      },
      response: { milliseconds: null, samples: [], observedAt: null },
    },
  ],
  systemState: {
    columns: [
      {
        projectId: 'spinner-api',
        projectName: 'Spinner API',
        environmentId: 'spinner-api-production',
        environmentName: 'Production',
      },
      {
        projectId: 'stalltrack',
        projectName: 'StallTrack',
        environmentId: 'stalltrack-production',
        environmentName: 'Production',
      },
      {
        projectId: 'amyl',
        projectName: 'AMYL',
        environmentId: 'amyl-local',
        environmentName: 'Local',
      },
    ],
    rows: [
      {
        key: 'api',
        label: 'API',
        cells: [
          { level: 'healthy', label: 'Healthy', detail: '103 ms' },
          { level: 'healthy', label: 'Healthy', detail: '91 ms' },
          { level: 'unknown', label: 'Not configured', detail: 'No health endpoint' },
        ],
      },
      {
        key: 'dependency:database',
        label: 'Database',
        cells: [
          { level: 'healthy', label: 'Healthy', detail: 'Reported by application' },
          { level: 'healthy', label: 'Healthy', detail: 'Reported by application' },
          null,
        ],
      },
      {
        key: 'dependency:redis',
        label: 'Redis',
        cells: [
          { level: 'healthy', label: 'Healthy', detail: 'Reported by application' },
          { level: 'healthy', label: 'Healthy', detail: 'Reported by application' },
          null,
        ],
      },
      {
        key: 'ci',
        label: 'CI',
        cells: [
          { level: 'healthy', label: 'Passed', detail: 'GitHub Actions' },
          { level: 'healthy', label: 'Passed', detail: 'GitHub Actions' },
          { level: 'notApplicable', label: 'N/A', detail: 'Workflow not configured' },
        ],
      },
      {
        key: 'versionSync',
        label: 'Version Sync',
        cells: [
          { level: 'healthy', label: 'In Sync', detail: '8a17c2f' },
          { level: 'healthy', label: 'In Sync', detail: '4f91b9a' },
          { level: 'unknown', label: 'Not configured', detail: 'No version endpoint' },
        ],
      },
    ],
  },
  activity: [
    {
      id: 'act-1',
      kind: 'healthRecovered',
      title: 'Spinner API health recovered',
      context: 'Production - 103 ms',
      occurredAt: '2026-08-13T18:22:00Z',
    },
    {
      id: 'act-2',
      kind: 'versionSynchronized',
      title: 'Spinner API version synchronized',
      context: 'Production - 8a17c2f',
      occurredAt: '2026-08-13T18:20:00Z',
    },
    {
      id: 'act-3',
      kind: 'versionDrift',
      title: 'StallTrack version drift detected',
      context: 'Production',
      occurredAt: '2026-08-13T17:36:00Z',
    },
    {
      id: 'act-4',
      kind: 'healthFailed',
      title: 'StallTrack health failed',
      context: 'Production',
      occurredAt: '2026-08-13T17:31:00Z',
    },
  ],
  summary: {
    level: 'warning',
    label: 'Partial visibility',
    uptime: null,
  },
};
