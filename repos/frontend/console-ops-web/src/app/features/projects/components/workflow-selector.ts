import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';

import { GitHubWorkflow, WorkflowRunConclusion } from '../../../core/contracts/github-discovery';
import { StatusLevel } from '../../../core/contracts/dashboard-overview';
import { RelativeTimePipe } from '../../../core/ui/relative-time.pipe';

/**
 * Words that make a workflow a plausible deployment workflow.
 *
 * Deliberately small and explicit. It only orders the list and marks one option `Suggested`; it never
 * selects anything, because the V1 contract forbids Console Ops choosing a workflow on its own.
 */
const DEPLOYMENT_HINTS: readonly string[] = ['deploy', 'release', 'publish', 'cd'];

interface WorkflowOption {
  readonly workflow: GitHubWorkflow;
  readonly suggested: boolean;
  readonly level: StatusLevel;
  readonly runLabel: string;
}

/** Lets the operator recognise and confirm which workflow deploys this environment. */
@Component({
  selector: 'co-workflow-selector',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RelativeTimePipe],
  templateUrl: './workflow-selector.html',
  styleUrl: './workflow-selector.scss',
})
export class WorkflowSelector {
  readonly workflows = input.required<readonly GitHubWorkflow[]>();
  /**
   * `undefined` means the operator has not chosen yet, so no option is selected. `null` is the explicit
   * "no deployment workflow" choice, and a string is the chosen workflow file.
   */
  readonly selected = input<string | null | undefined>(undefined);

  readonly selectWorkflow = output<string | null>();

  /** Set when the operator asks to change a choice they already made. */
  private readonly reopened = signal(false);

  /** Reference instant for "last run" times: the browser clock is the only clock available here. */
  protected readonly now = new Date().toISOString();

  /** The list stays open until a choice exists, then collapses to that choice. */
  protected readonly showList = computed(() => this.selected() === undefined || this.reopened());

  protected readonly chosen = computed<WorkflowOption | null>(() => {
    const selected = this.selected();
    if (selected === undefined || selected === null) {
      return null;
    }
    return this.options().find((option) => option.workflow.fileName === selected) ?? null;
  });

  protected readonly chosenNone = computed(() => this.selected() === null);

  protected choose(fileName: string | null): void {
    this.reopened.set(false);
    this.selectWorkflow.emit(fileName);
  }

  protected reopen(): void {
    this.reopened.set(true);
  }

  protected readonly options = computed<readonly WorkflowOption[]>(() => {
    const workflows = [...this.workflows()].filter((workflow) => workflow.active);
    const suggestedName = firstDeploymentCandidate(workflows);

    return workflows
      .map((workflow) => ({
        workflow,
        suggested: workflow.fileName === suggestedName,
        level: levelFor(workflow.latestRunConclusion),
        runLabel: runLabelFor(workflow.latestRunConclusion),
      }))
      .sort((left, right) => Number(right.suggested) - Number(left.suggested));
  });

  protected readonly hasSuggestion = computed(() =>
    this.options().some((option) => option.suggested),
  );
}

function firstDeploymentCandidate(workflows: readonly GitHubWorkflow[]): string | null {
  const match = workflows.find((workflow) => {
    const haystack = `${workflow.name} ${workflow.fileName}`.toLowerCase();
    return DEPLOYMENT_HINTS.some((hint) => haystack.includes(hint));
  });

  return match?.fileName ?? null;
}

function levelFor(conclusion: WorkflowRunConclusion): StatusLevel {
  switch (conclusion) {
    case 'success':
      return 'healthy';
    case 'failure':
      return 'down';
    case 'inProgress':
      return 'running';
    case 'cancelled':
      return 'warning';
    default:
      return 'unknown';
  }
}

function runLabelFor(conclusion: WorkflowRunConclusion): string {
  switch (conclusion) {
    case 'success':
      return 'Passed';
    case 'failure':
      return 'Failed';
    case 'inProgress':
      return 'Running';
    case 'cancelled':
      return 'Cancelled';
    case 'never':
      return 'Never run';
    default:
      return 'Unknown';
  }
}
