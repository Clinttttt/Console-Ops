import { Observable } from 'rxjs';

import {
  ManualRunSupportReading,
  WorkflowInventory,
  WorkflowRunHistory,
  WorkflowRunJob,
  WorkflowRiskLevel,
  WorkflowDispatchAccepted,
  WorkflowBranches,
  WorkflowRiskReading,
} from '../contracts/workflows';

/**
 * Port for the Workflows screen.
 *
 * Read-only: the browser asks Console Ops, and Console Ops asks the provider. No token, repository call, or
 * classification decision happens here.
 */
export abstract class WorkflowsDataSource {
  abstract load(): Observable<WorkflowInventory>;

  /**
   * The jobs of one run, read for the workflow an operator selected.
   *
   * Separate from the inventory because each run costs the provider a request: reading jobs for every workflow
   * on the page would multiply the cost of opening the screen to answer a question nobody asked yet.
   */
  abstract loadRunJobs(projectId: string, runId: string): Observable<readonly WorkflowRunJob[]>;

  /** Recent runs of one workflow, read when an operator asks for its history. */
  abstract loadRuns(
    projectId: string,
    workflowId: string,
    limit?: number,
  ): Observable<WorkflowRunHistory>;

  /**
   * Whether one workflow declares a manual dispatch trigger.
   *
   * Read on selection because the answer costs one request for the workflow's definition, which the inventory
   * deliberately does not spend for every workflow on the page.
   */
  /**
   * Records how much intent starting this workflow should require.
   *
   * The only write on this screen. Setting `unclassified` withdraws the decision, which returns the workflow to
   * not being executable.
   */
  abstract setRisk(
    projectId: string,
    workflowPath: string,
    level: WorkflowRiskLevel,
  ): Observable<WorkflowRiskReading>;

  /**
   * Asks Console Ops to start a workflow.
   *
   * Every gate is re-checked by the API, so a screen that offered a run it should not have is still refused.
   */
  /** The refs a run could target, read when a run is being asked for. */
  abstract loadBranches(projectId: string): Observable<WorkflowBranches>;

  abstract dispatch(
    projectId: string,
    workflowId: string,
    request: {
      readonly reference: string;
      readonly inputs: Readonly<Record<string, string>>;
      readonly confirmation: string | null;
    },
  ): Observable<WorkflowDispatchAccepted>;

  abstract loadManualRunSupport(
    projectId: string,
    workflowId: string,
    workflowPath: string,
  ): Observable<ManualRunSupportReading>;
}
