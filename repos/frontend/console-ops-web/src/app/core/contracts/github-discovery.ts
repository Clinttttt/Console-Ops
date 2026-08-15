/**
 * Typed contract for GitHub discovery, phases 1 and 2 of
 * `docs/Console_Ops_Add_Project_Import_Plan.md`.
 *
 * ```text
 * GET /api/github/repositories?query=
 * GET /api/github/repositories/{owner}/{name}/workflows
 * ```
 *
 * Both are provider reads, not stored entities: Console Ops asks GitHub what exists so the operator
 * does not retype it. The endpoints are not implemented yet. The frontend therefore treats discovery as
 * an optional capability and keeps the manual path working when it is unavailable, rather than showing
 * invented repositories.
 *
 * Standard contract rules: plain JSON, camel case, ISO-8601 UTC instants, `null` for anything the
 * provider did not report. Credentials never appear in a response.
 */

export interface GitHubRepository {
  /** GitHub account or organisation that owns the repository. */
  readonly owner: string;
  readonly name: string;
  readonly defaultBranch: string;
  /** `true` when the repository is private to the configured credential. */
  readonly isPrivate: boolean;
  /** Primary language when GitHub reports one. */
  readonly language: string | null;
  /** Last push time, used to order and date the list. `null` when GitHub omits it. */
  readonly pushedAt: string | null;
  /** Absolute repository URL for an outbound link. */
  readonly htmlUrl: string | null;
}

export interface GitHubRepositoryPage {
  readonly repositories: readonly GitHubRepository[];
  /** `true` when GitHub reported further pages the operator has not seen. */
  readonly hasMore: boolean;
}

/** Outcome of the latest run of a workflow, as GitHub reported it. */
export type WorkflowRunConclusion =
  'success' | 'failure' | 'cancelled' | 'inProgress' | 'unknown' | 'never';

export interface GitHubWorkflow {
  readonly name: string;
  /** Workflow file path, such as `.github/workflows/deploy-production.yml`. */
  readonly path: string;
  /** File name only, which is what the V1 project contract stores. */
  readonly fileName: string;
  /** `false` for workflows GitHub reports as disabled. */
  readonly active: boolean;
  readonly latestRunConclusion: WorkflowRunConclusion;
  readonly latestRunCompletedAt: string | null;
}

export interface GitHubWorkflowList {
  readonly workflows: readonly GitHubWorkflow[];
}

/**
 * Head commit of the imported branch.
 *
 * Read for one chosen repository, never for a list, so importing does not make a request per row.
 */
export interface GitHubLatestCommit {
  readonly commitSha: string;
  readonly commitShortSha: string;
  readonly committedAt: string | null;
}

export type DetectedEndpointKind = 'health' | 'version';

/**
 * An endpoint path recognised in repository source.
 *
 * A detection, not configuration: it is offered as a suggestion the operator confirms, and only a probe
 * proves the endpoint answers.
 */
export interface DetectedEndpoint {
  readonly kind: DetectedEndpointKind;
  readonly path: string;
  /** Repository file the path was read from, so the operator can check it. */
  readonly sourceFile: string;
}

export interface DetectedEndpoints {
  readonly endpoints: readonly DetectedEndpoint[];
  /** Separates "nothing found" from "nothing read". */
  readonly inspectedFileCount: number;
}
