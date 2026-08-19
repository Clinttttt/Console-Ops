import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';

import { Workflow, WorkflowInventory, WorkflowProjectGroup } from '../../contracts/workflows';
import { WorkflowsDataSource } from '../workflows.data-source';

/**
 * Sample data for the Workflows screen, while the screen is designed.
 *
 * Labelled at the source: the inventory carries `isSampleData: true` and the screen states it. Deleted when
 * workflows are read from the provider - this exists to settle layout and wording, not to describe automation
 * that runs.
 *
 * The values are chosen to exercise what the screen must get right rather than to look full: a workflow that is
 * explicitly a deployment and one that is running, a workflow with no run at all, one whose manual dispatch the
 * provider has not reported, a failed run whose failing job is identifiable, a disabled workflow that must not
 * read as failed, and a second project so grouping and the project filter are real.
 */
const READ_AT = '2026-08-19T07:05:00.000Z';

const EEMO_WORKFLOWS: readonly Workflow[] = [
  {
    id: 'eemo-deploy-production',
    name: 'Deploy production',
    path: '.github/workflows/deploy-production.yml',
    state: 'active',
    classification: 'deployment',
    primaryDeploymentFor: 'Production',
    manualRun: 'supported',
    latestRun: {
      id: 'eemo-deploy-535',
      number: 535,
      status: 'inProgress',
      conclusion: null,
      branch: 'master',
      commitSha: '2ac8bf0f4c1e9d7a3b5c8e2f1a4d6b9c0e3f7a21',
      commitShortSha: '2ac8bf0',
      trigger: 'push',
      actor: 'Clinttttt',
      startedAt: '2026-08-19T06:57:00.000Z',
      completedAt: null,
      durationSeconds: null,
      jobs: [
        { name: 'Prepare', status: 'completed', conclusion: 'passed', durationSeconds: 72 },
        { name: 'Deploy', status: 'inProgress', conclusion: null, durationSeconds: null },
        { name: 'Verify', status: 'waiting', conclusion: null, durationSeconds: null },
      ],
    },
  },
  {
    id: 'eemo-ci',
    name: 'CI',
    path: '.github/workflows/ci.yml',
    state: 'active',
    classification: 'unclassified',
    primaryDeploymentFor: null,
    // The provider reports no manual dispatch for this workflow, so no Run action may be offered.
    manualRun: 'unavailable',
    latestRun: {
      id: 'eemo-ci-938',
      number: 938,
      status: 'completed',
      conclusion: 'passed',
      branch: 'master',
      commitSha: '4b09f8027d3e5a1c9f4b7d2e8a3c6f0b5d9e1a47',
      commitShortSha: '4b09f80',
      trigger: 'push',
      actor: 'Clinttttt',
      startedAt: '2026-08-19T05:01:00.000Z',
      completedAt: '2026-08-19T05:05:18.000Z',
      durationSeconds: 258,
      jobs: [
        { name: 'Backend', status: 'completed', conclusion: 'passed', durationSeconds: 161 },
        { name: 'Frontend', status: 'completed', conclusion: 'passed', durationSeconds: 97 },
      ],
    },
  },
  {
    id: 'eemo-database-backup',
    name: 'Database backup',
    path: '.github/workflows/database-backup.yml',
    state: 'active',
    classification: 'unclassified',
    primaryDeploymentFor: null,
    manualRun: 'supported',
    latestRun: {
      id: 'eemo-backup-212',
      number: 212,
      status: 'completed',
      conclusion: 'passed',
      branch: 'master',
      commitSha: '89ad1c2e7f4b0d9a3c6e2f8b1d5a7c4e0f3b9d62',
      commitShortSha: '89ad1c2',
      trigger: 'schedule',
      actor: 'github-actions',
      startedAt: '2026-08-19T02:00:00.000Z',
      completedAt: '2026-08-19T02:03:41.000Z',
      durationSeconds: 221,
      jobs: [{ name: 'Backup', status: 'completed', conclusion: 'passed', durationSeconds: 221 }],
    },
  },
  {
    id: 'eemo-database-restore',
    name: 'Database restore',
    path: '.github/workflows/database-restore.yml',
    state: 'active',
    classification: 'unclassified',
    primaryDeploymentFor: null,
    manualRun: 'supported',
    // Never run: distinct from a run that failed, and the screen must not fill the gap with a status.
    latestRun: null,
  },
  {
    id: 'eemo-backup-freshness',
    name: 'Backup freshness',
    path: '.github/workflows/backup-freshness.yml',
    state: 'active',
    classification: 'unclassified',
    primaryDeploymentFor: null,
    manualRun: 'unavailable',
    latestRun: {
      id: 'eemo-freshness-88',
      number: 88,
      status: 'completed',
      conclusion: 'failed',
      branch: 'master',
      commitSha: 'd3f7a9105c2e8b4d7a1f6c3e9b0d5a8f2c4e7b13',
      commitShortSha: 'd3f7a91',
      trigger: 'schedule',
      actor: 'github-actions',
      startedAt: '2026-08-19T01:00:00.000Z',
      completedAt: '2026-08-19T01:01:12.000Z',
      durationSeconds: 72,
      jobs: [
        { name: 'Check age', status: 'completed', conclusion: 'passed', durationSeconds: 18 },
        {
          name: 'Assert freshness',
          status: 'completed',
          conclusion: 'failed',
          durationSeconds: 54,
        },
      ],
    },
  },
  {
    id: 'eemo-publish-collector-apk',
    name: 'Publish Collector APK',
    path: '.github/workflows/publish-collector-apk.yml',
    state: 'active',
    classification: 'unclassified',
    primaryDeploymentFor: null,
    // The provider has not been asked about this one, which is not the same as knowing it cannot be run.
    manualRun: 'unknown',
    latestRun: {
      id: 'eemo-apk-47',
      number: 47,
      status: 'completed',
      conclusion: 'passed',
      branch: 'master',
      commitSha: 'f8e12d4b6a3c9e0f5d2b8a1c7e4f0b3d9a6c2e58',
      commitShortSha: 'f8e12d4',
      trigger: 'push',
      actor: 'Clinttttt',
      startedAt: '2026-08-18T07:12:00.000Z',
      completedAt: '2026-08-18T07:20:36.000Z',
      durationSeconds: 516,
      jobs: [
        { name: 'Build APK', status: 'completed', conclusion: 'passed', durationSeconds: 516 },
      ],
    },
  },
];

const SPINNER_WORKFLOWS: readonly Workflow[] = [
  {
    id: 'spinner-deploy-staging',
    name: 'Deploy staging',
    path: '.github/workflows/deploy-staging.yml',
    state: 'active',
    classification: 'deployment',
    primaryDeploymentFor: 'Staging',
    manualRun: 'supported',
    latestRun: {
      id: 'spinner-deploy-35',
      number: 35,
      status: 'completed',
      conclusion: 'passed',
      branch: 'main',
      commitSha: '280dd8617c4e2b9a5f3d0c8e1b6a4f7d2c9e3b05',
      commitShortSha: '280dd86',
      trigger: 'push',
      actor: 'Clinttttt',
      startedAt: '2026-08-19T04:54:00.000Z',
      completedAt: '2026-08-19T04:56:18.000Z',
      durationSeconds: 138,
      jobs: [
        { name: 'Build image', status: 'completed', conclusion: 'passed', durationSeconds: 96 },
        { name: 'Update revision', status: 'completed', conclusion: 'passed', durationSeconds: 42 },
      ],
    },
  },
  {
    id: 'spinner-security-scan',
    name: 'Security scan',
    // Disabled at the provider. Reported as disabled, never as failed, and never quietly hidden.
    path: '.github/workflows/security-scan.yml',
    state: 'disabled',
    classification: 'unclassified',
    primaryDeploymentFor: null,
    manualRun: 'unavailable',
    latestRun: {
      id: 'spinner-scan-19',
      number: 19,
      status: 'completed',
      conclusion: 'cancelled',
      branch: 'main',
      commitSha: '1d92cab4f7e0b3a9c6d2e8f1b5a7c4d0e3f9b628',
      commitShortSha: '1d92cab',
      trigger: 'manual',
      actor: 'Clinttttt',
      startedAt: '2026-08-15T09:30:00.000Z',
      completedAt: '2026-08-15T09:31:04.000Z',
      durationSeconds: 64,
      jobs: [{ name: 'Scan', status: 'completed', conclusion: 'cancelled', durationSeconds: 64 }],
    },
  },
];

const GROUPS: readonly WorkflowProjectGroup[] = [
  {
    projectId: 'eemo-cantilan-sds',
    projectName: 'EEMO-Cantilan-SDS',
    repository: 'Clinttttt/EEMO-Cantilan-SDS',
    workflows: EEMO_WORKFLOWS,
  },
  {
    projectId: 'spinner',
    projectName: 'Spinner API',
    repository: 'Clinttttt/Spinner',
    workflows: SPINNER_WORKFLOWS,
  },
];

@Injectable()
export class MockWorkflowsDataSource extends WorkflowsDataSource {
  load(): Observable<WorkflowInventory> {
    // Answered synchronously: a simulated delay would only make every screen and test wait for nothing.
    return of<WorkflowInventory>({
      isSampleData: true,
      readAt: READ_AT,
      groups: GROUPS,
    });
  }
}
