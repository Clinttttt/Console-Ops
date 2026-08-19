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
export type WorkflowRunStatus = 'queued' | 'inProgress' | 'waiting' | 'completed' | 'unknown';

/**
 * How a completed run ended. `null` while it has not.
 *
 * Kept wider than passed and failed because the provider distinguishes more than that, and collapsing
 * `cancelled` or `actionRequired` into "failed" would report a cause that was never observed.
 */
export type WorkflowRunConclusion =
  'passed' | 'failed' | 'cancelled' | 'skipped' | 'timedOut' | 'actionRequired' | 'neutral';

/**
 * What started a run, as the provider's own event: `push`, `schedule`, `workflow_dispatch`, and anything else
 * it reports.
 *
 * A plain string rather than a union, because the set is the provider's and a closed list would force an
 * unfamiliar event to be cast to a familiar one. Never read as evidence of a workflow's purpose: a deployment
 * can run on push exactly as a test suite can.
 */
export type WorkflowTrigger = string;

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
  /**
   * The provider's own run number, shown as `#535` because that is how it is referred to elsewhere.
   *
   * `null` when the provider did not report one, rather than a zero that would read as a real run.
   */
  readonly number: number | null;
  readonly status: WorkflowRunStatus;
  readonly conclusion: WorkflowRunConclusion | null;
  readonly branch: string;
  readonly commitSha: string;
  readonly commitShortSha: string;
  readonly trigger: WorkflowTrigger;
  /** `null` when the provider did not say who started it - a scheduled run often has no person behind it. */
  readonly actor: string | null;
  /** ISO-8601 UTC, or `null` when the provider reported no start. */
  readonly startedAt: string | null;
  readonly completedAt: string | null;
  readonly durationSeconds: number | null;
  /**
   * The provider's own page for this run, or `null` when it reported none.
   *
   * Carried so an operator can reach the provider for what Console Ops does not show, rather than composing a
   * URL from ids and hoping it resolves.
   */
  readonly runUrl: string | null;
  readonly jobs: readonly WorkflowRunJob[];
}

export interface Workflow {
  readonly id: string;
  readonly name: string;
  /** Where the definition lives, such as `.github/workflows/deploy-production.yml`. */
  readonly path: string;
  readonly state: WorkflowState;
  readonly classification: WorkflowClassification;

  readonly manualRun: ManualRunSupport;
  /** `null` when the provider has recorded no run, which is not the same as a run that failed. */
  readonly latestRun: WorkflowRun | null;
}

/**
 * Why a project's workflows could not be read, or `null` when they were.
 *
 * Present because one unreadable repository must not look like a repository with no automation: an operator
 * would go looking for missing workflows instead of a rejected token.
 */
export type WorkflowReadFailure =
  'unauthorized' | 'notFound' | 'rateLimited' | 'invalidResponse' | 'unavailable';

/** Workflows belong to a repository, and the screen groups by the project that owns it. */
export interface WorkflowProjectGroup {
  readonly projectId: string;
  readonly projectName: string;
  readonly repository: string;
  readonly workflows: readonly Workflow[];
  readonly readFailure: WorkflowReadFailure | null;
}

export interface WorkflowInventory {
  /**
   * `true` while these are examples rather than provider facts, so the screen can say so.
   *
   * Deleted with the sample adapter once the inventory is read from a provider. It is carried here rather than
   * decided in the page so that no screen can present sample data as observed automation.
   */
  readonly isSampleData: boolean;
  /** ISO-8601 UTC: when the provider was asked. These are live facts, not stored observations. */
  readonly readAt: string;
  readonly groups: readonly WorkflowProjectGroup[];
}
