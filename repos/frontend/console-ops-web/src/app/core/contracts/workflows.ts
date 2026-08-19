/**
 * Repository automation as a provider reports it.
 *
 * Workflows and Deployments are different concepts and stay that way: Deployments answers "what release moved
 * into an environment", while this answers "what automation exists and how did it execute". The provider owns
 * every fact here - a workflow's existence, its run state, its jobs - so nothing in this contract can be
 * derived by Console Ops from a name.
 */

/**
 * What Console Ops can honestly say a workflow is for.
 *
 * Only `deployment` is a claim, and only because an operator explicitly configured that workflow as an
 * environment's primary deployment workflow. Everything else is `unclassified`: a provider does not report a
 * business category, and deciding that "Database backup" is maintenance because of its name would be a guess
 * presented as a fact.
 */
export type WorkflowClassification = 'deployment' | 'unclassified';

/** Whether the provider still exposes this workflow for execution. Disabled is not a failure. */
export type WorkflowState = 'active' | 'disabled';

/** Where a run is in its life, as distinct from how it ended. */
export type WorkflowRunStatus = 'queued' | 'inProgress' | 'waiting' | 'completed';

/**
 * How a completed run ended. `null` while it has not.
 *
 * Kept wider than passed and failed because the provider distinguishes more than that, and collapsing
 * `cancelled` or `actionRequired` into "failed" would report a cause that was never observed.
 */
export type WorkflowRunConclusion =
  'passed' | 'failed' | 'cancelled' | 'skipped' | 'timedOut' | 'actionRequired' | 'neutral';

/** What started a run. Carried as the provider's own event, never read as evidence of the workflow's purpose. */
export type WorkflowTrigger = 'push' | 'pullRequest' | 'schedule' | 'manual' | 'workflowCall';

/**
 * Whether this workflow can be started from Console Ops.
 *
 * `unknown` is a real answer: it means the provider has not been asked or did not say, which is different from
 * knowing that manual dispatch is unavailable.
 */
export type ManualRunSupport = 'supported' | 'unavailable' | 'unknown';

/** One job within a run, so "where is this stuck" is answerable without opening the provider. */
export interface WorkflowRunJob {
  readonly name: string;
  readonly status: WorkflowRunStatus;
  readonly conclusion: WorkflowRunConclusion | null;
  /** `null` while the job has not finished, or when the provider reported no timing. */
  readonly durationSeconds: number | null;
}

export interface WorkflowRun {
  readonly id: string;
  /** The provider's own run number, shown as `#535` because that is how it is referred to elsewhere. */
  readonly number: number;
  readonly status: WorkflowRunStatus;
  readonly conclusion: WorkflowRunConclusion | null;
  readonly branch: string;
  readonly commitSha: string;
  readonly commitShortSha: string;
  readonly trigger: WorkflowTrigger;
  readonly actor: string;
  /** ISO-8601 UTC. */
  readonly startedAt: string;
  readonly completedAt: string | null;
  readonly durationSeconds: number | null;
  readonly jobs: readonly WorkflowRunJob[];
}

export interface Workflow {
  readonly id: string;
  readonly name: string;
  /** Where the definition lives, such as `.github/workflows/deploy-production.yml`. */
  readonly path: string;
  readonly state: WorkflowState;
  readonly classification: WorkflowClassification;
  /**
   * The environment this workflow is the primary deployment workflow for, or `null`.
   *
   * A repository has many workflows; an environment has zero or one. This is the only reason a workflow may be
   * called a deployment, and it is why the label names the environment rather than standing alone.
   */
  readonly primaryDeploymentFor: string | null;
  readonly manualRun: ManualRunSupport;
  /** `null` when the provider has recorded no run, which is not the same as a run that failed. */
  readonly latestRun: WorkflowRun | null;
}

/** Workflows belong to a repository, and the screen groups by the project that owns it. */
export interface WorkflowProjectGroup {
  readonly projectId: string;
  readonly projectName: string;
  readonly repository: string;
  readonly workflows: readonly Workflow[];
}

export interface WorkflowInventory {
  /**
   * `true` while these are examples rather than provider facts, so the screen can say so.
   *
   * Deleted with the sample adapter once the inventory is read from a provider. It is carried here rather than
   * decided in the page so that no screen can present sample data as observed automation.
   */
  readonly isSampleData: boolean;
  /** ISO-8601 UTC: when the inventory was read. */
  readonly readAt: string;
  readonly groups: readonly WorkflowProjectGroup[];
}
