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

/** One step within a job. The provider numbers them, so an order is reported rather than assumed. */
export interface WorkflowRunStep {
  readonly name: string;
  readonly number: number | null;
  readonly status: WorkflowRunStatus;
  readonly conclusion: WorkflowRunConclusion | null;
  readonly durationSeconds: number | null;
}

/** One job within a run, so "where is this stuck" is answerable without opening the provider. */
export interface WorkflowRunJob {
  readonly name: string;
  readonly status: WorkflowRunStatus;
  readonly conclusion: WorkflowRunConclusion | null;
  /** `null` while the job has not finished, or when the provider reported no timing. */
  readonly durationSeconds: number | null;
  /**
   * The step the provider reported as failed, or `null` when none did.
   *
   * A job that failed without any step failing - a runner that died, a cancelled queue - names none rather than
   * blaming a step that reported success.
   */
  readonly failedStep: string | null;
  /** Empty while the job has not started, which is not the same as a job that ran nothing. */
  readonly steps: readonly WorkflowRunStep[];
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

/**
 * How much intent starting a workflow should require, as an operator marked it.
 *
 * Never derived. `unclassified` is the default and a real state: Console Ops will not run a workflow whose risk
 * nobody has stated, because a name cannot prove that one drops a database.
 */
export type WorkflowRiskLevel = 'unclassified' | 'normal' | 'destructive';

export interface Workflow {
  readonly id: string;
  readonly name: string;
  /** Where the definition lives, such as `.github/workflows/deploy-production.yml`. */
  readonly path: string;
  readonly state: WorkflowState;
  readonly classification: WorkflowClassification;

  readonly manualRun: ManualRunSupport;
  readonly risk: WorkflowRiskLevel;
  /** When an operator marked it, or `null` when nobody has. */
  readonly riskDecidedAt: string | null;
  /**
   * Whether anything Console Ops has stored refuses to run this: `false` while the risk is unclassified, and
   * `false` for a workflow the provider has disabled.
   *
   * Decided by the API rather than re-derived here. It does not cover whether the provider accepts a manual
   * dispatch - that is read from the definition on selection, because reading it for every workflow would double
   * the cost of opening the screen.
   */
  readonly executable: boolean;
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
  /** The project's registered branch. A run defaults to it and requires an explicit change. */
  readonly defaultBranch: string;
  readonly workflows: readonly Workflow[];
  readonly readFailure: WorkflowReadFailure | null;
}

/**
 * Recent runs of one workflow, newest first.
 *
 * `hasMore` is carried so the screen says this is recent history rather than everything the workflow has done:
 * a list that looks complete when it is one page would misrepresent a workflow with years behind it.
 */
export interface WorkflowRunHistory {
  readonly workflowId: string;
  readonly runs: readonly WorkflowRun[];
  readonly hasMore: boolean;
}

/**
 * Whether one workflow can be started manually, established from its own definition.
 *
 * The inventory cannot answer this: a dispatch trigger is declared in the workflow file, and GitHub's listing
 * does not report triggers. Read for the workflow an operator selected, so a page of workflows does not cost a
 * page of extra requests.
 */
/** One input the workflow declares for a manual run. Nothing here is invented by Console Ops. */
export interface WorkflowInput {
  readonly name: string;
  readonly description: string | null;
  readonly required: boolean;
  /** `string`, `choice`, `boolean`, or `environment`, as declared. */
  readonly type: string;
  readonly default: string | null;
  /** Allowed values where the workflow declared them, otherwise empty. */
  readonly options: readonly string[];
}

export interface ManualRunSupportReading {
  readonly manualRun: ManualRunSupport;
  /** The file the answer was read from, so the claim can be checked. */
  readonly definitionPath: string;
  readonly inputs: readonly WorkflowInput[];
}

/**
 * The refs a run could target.
 *
 * Read from the repository so a branch is chosen from what exists. `hasMore` is carried because the page is
 * bounded, and the registered default is always present even when the provider no longer lists it.
 */
export interface WorkflowBranches {
  readonly defaultBranch: string;
  readonly branches: readonly string[];
  readonly hasMore: boolean;
}

/**
 * What the provider said when asked to start a workflow.
 *
 * `requested` is the only status: the provider accepts a dispatch without reporting a run, so Console Ops does not
 * know which run it started and finds it afterwards rather than claiming one.
 */
export interface WorkflowDispatchAccepted {
  readonly status: 'requested';
  readonly workflowId: string;
  readonly reference: string;
  readonly requestedAt: string;
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
