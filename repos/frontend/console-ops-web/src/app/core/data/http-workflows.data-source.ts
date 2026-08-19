import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import {
  ManualRunSupport,
  ManualRunSupportReading,
  Workflow,
  WorkflowClassification,
  WorkflowInventory,
  WorkflowProjectGroup,
  WorkflowReadFailure,
  WorkflowRun,
  WorkflowRunConclusion,
  WorkflowRunHistory,
  WorkflowRunJob,
  WorkflowRunStatus,
  WorkflowState,
  WorkflowTrigger,
} from '../contracts/workflows';
import { WorkflowsDataSource } from './workflows.data-source';

/** The wire shape of `GET /api/workflows`. */
interface WorkflowInventoryPayload {
  readonly readAt: string;
  readonly groups: readonly {
    readonly projectId: string;
    readonly projectName: string;
    readonly repository: string;
    readonly workflows: readonly WorkflowPayload[];
    readonly readFailure: string | null;
  }[];
}

interface WorkflowPayload {
  readonly id: string;
  readonly name: string;
  readonly path: string;
  readonly state: string;
  readonly classification: string;
  readonly manualRun: string;
  readonly latestRun: RunPayload | null;
}

interface RunPayload {
  readonly id: string;
  readonly number: number | null;
  readonly status: string;
  readonly conclusion: string | null;
  readonly branch: string;
  readonly commitSha: string;
  readonly commitShortSha: string;
  readonly trigger: string;
  readonly actor: string | null;
  readonly startedAt: string | null;
  readonly completedAt: string | null;
  readonly durationSeconds: number | null;
  readonly runUrl: string | null;
  readonly jobs: readonly JobPayload[];
}

interface JobPayload {
  readonly name: string;
  readonly status: string;
  readonly conclusion: string | null;
  readonly durationSeconds: number | null;
}

/** The wire shape of `GET /api/workflows/projects/{id}/workflows/{id}/runs`. */
interface RunsPayload {
  readonly workflowId: string;
  readonly runs: readonly RunPayload[];
  readonly hasMore: boolean;
}

/** The wire shape of `GET /api/workflows/projects/{id}/workflows/{id}/manual-run`. */
interface ManualRunPayload {
  readonly manualRun: string;
  readonly definitionPath: string;
}

interface RunJobsPayload {
  readonly runId: string;
  readonly jobs: readonly JobPayload[];
}

/**
 * Reads workflows from Console Ops, which reads them from the provider.
 *
 * The browser never talks to GitHub: it holds no token, and a screen that asked a provider directly would put
 * one there. Jobs are a second read, made only for the workflow an operator selected.
 */
@Injectable()
export class HttpWorkflowsDataSource extends WorkflowsDataSource {
  private readonly http = inject(HttpClient);

  override load(): Observable<WorkflowInventory> {
    return this.http.get<WorkflowInventoryPayload>('/api/workflows').pipe(
      map((payload) => ({
        // Read from the provider during the request, so nothing here is sample data.
        isSampleData: false,
        readAt: payload.readAt,
        groups: payload.groups.map(toGroup),
      })),
    );
  }

  override loadRunJobs(projectId: string, runId: string): Observable<readonly WorkflowRunJob[]> {
    return this.http
      .get<RunJobsPayload>(
        `/api/workflows/projects/${encodeURIComponent(projectId)}/runs/${encodeURIComponent(runId)}/jobs`,
      )
      .pipe(map((payload) => payload.jobs.map(toJob)));
  }

  override loadManualRunSupport(
    projectId: string,
    workflowId: string,
    workflowPath: string,
  ): Observable<ManualRunSupportReading> {
    return this.http
      .get<ManualRunPayload>(
        `/api/workflows/projects/${encodeURIComponent(projectId)}/workflows/${encodeURIComponent(
          workflowId,
        )}/manual-run`,
        { params: { path: workflowPath } },
      )
      .pipe(
        map((payload) => ({
          manualRun: toManualRun(payload.manualRun),
          definitionPath: payload.definitionPath,
        })),
      );
  }

  override loadRuns(projectId: string, workflowId: string): Observable<WorkflowRunHistory> {
    return this.http
      .get<RunsPayload>(
        `/api/workflows/projects/${encodeURIComponent(projectId)}/workflows/${encodeURIComponent(
          workflowId,
        )}/runs`,
      )
      .pipe(
        map((payload) => ({
          workflowId: payload.workflowId,
          runs: payload.runs.map(toRun),
          hasMore: payload.hasMore,
        })),
      );
  }
}

function toGroup(payload: WorkflowInventoryPayload['groups'][number]): WorkflowProjectGroup {
  return {
    projectId: payload.projectId,
    projectName: payload.projectName,
    repository: payload.repository,
    workflows: payload.workflows.map(toWorkflow),
    readFailure: toReadFailure(payload.readFailure),
  };
}

function toWorkflow(payload: WorkflowPayload): Workflow {
  return {
    id: payload.id,
    name: payload.name,
    path: payload.path,
    state: payload.state === 'disabled' ? 'disabled' : ('active' as WorkflowState),
    classification: toClassification(payload.classification),
    manualRun: toManualRun(payload.manualRun),
    latestRun: payload.latestRun === null ? null : toRun(payload.latestRun),
  };
}

function toRun(payload: RunPayload): WorkflowRun {
  return {
    id: payload.id,
    number: payload.number,
    status: toStatus(payload.status),
    conclusion: toConclusion(payload.conclusion),
    branch: payload.branch,
    commitSha: payload.commitSha,
    commitShortSha: payload.commitShortSha,
    trigger: toTrigger(payload.trigger),
    actor: payload.actor,
    startedAt: payload.startedAt,
    completedAt: payload.completedAt,
    durationSeconds: payload.durationSeconds,
    runUrl: payload.runUrl,
    jobs: payload.jobs.map(toJob),
  };
}

function toJob(payload: JobPayload): WorkflowRunJob {
  return {
    name: payload.name,
    status: toStatus(payload.status),
    conclusion: toConclusion(payload.conclusion),
    durationSeconds: payload.durationSeconds,
  };
}

/**
 * An unrecognised value reads as unknown rather than as anything reassuring.
 *
 * The API and this contract are versioned together, so this only matters if they ever drift - and a drift that
 * silently renders as `Passed` is the one that would cost an operator something.
 */
function toStatus(status: string): WorkflowRunStatus {
  switch (status) {
    case 'queued':
    case 'inProgress':
    case 'waiting':
    case 'completed':
      return status;
    default:
      return 'unknown';
  }
}

function toConclusion(conclusion: string | null): WorkflowRunConclusion | null {
  switch (conclusion) {
    case 'passed':
    case 'failed':
    case 'cancelled':
    case 'skipped':
    case 'timedOut':
    case 'actionRequired':
    case 'neutral':
      return conclusion;
    default:
      return null;
  }
}

/** Anything other than an explicit deployment stays unclassified: the screen never promotes a workflow. */
function toClassification(classification: string): WorkflowClassification {
  return classification === 'deployment' ? 'deployment' : 'unclassified';
}

function toManualRun(manualRun: string): ManualRunSupport {
  switch (manualRun) {
    case 'supported':
    case 'unavailable':
      return manualRun;
    default:
      return 'unknown';
  }
}

function toTrigger(trigger: string): WorkflowTrigger {
  switch (trigger) {
    case 'push':
    case 'schedule':
    case 'workflowCall':
      return trigger;
    case 'pull_request':
      return 'pullRequest';
    case 'workflow_dispatch':
      return 'manual';
    case 'workflow_call':
      return 'workflowCall';
    default:
      // The provider's own event, kept as it was reported rather than mapped to something familiar.
      return trigger as WorkflowTrigger;
  }
}

function toReadFailure(failure: string | null): WorkflowReadFailure | null {
  switch (failure) {
    case 'unauthorized':
    case 'notFound':
    case 'rateLimited':
    case 'invalidResponse':
      return failure;
    case null:
      return null;
    default:
      return 'unavailable';
  }
}
