import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';

import {
  Workflow,
  WorkflowClassification,
  WorkflowRiskLevel,
} from '../../core/contracts/workflows';
import {
  ACTIVE_PROVIDER_REFRESH_INTERVAL_MS,
  IDLE_PROVIDER_REFRESH_INTERVAL_MS,
  providerRefresh,
} from '../../core/state/auto-refresh';
import { WorkflowsStore } from '../../core/state/workflows.store';
import { Icon } from '../../core/ui/icon';
import { ProjectMark, ProjectMarkTone } from '../../core/ui/project-mark';
import { toneForProject } from '../../core/ui/project-tone';
import { WorkflowDetail } from './components/workflow-detail';
import { WorkflowRunDialog } from './components/workflow-run-dialog';
import { WorkflowRow } from './components/workflow-row';

/** Which workflows the inventory is narrowed to. `null` is everything. */
type TypeFilter = WorkflowClassification | null;

/** A project group after filtering, carrying the project so the row can be attributed. */
interface FilteredGroup {
  readonly projectId: string;
  readonly projectName: string;
  readonly workflows: readonly Workflow[];
  readonly readFailure: string | null;
}

/**
 * Workflows.
 *
 * What automation exists in the connected repositories and how it last executed. Deliberately not a second
 * Deployments screen: Deployments answers which release reached an environment, while this answers what ran.
 *
 * Nothing here is classified by Console Ops. A workflow reads as a deployment only where an operator configured
 * it as an environment's primary deployment workflow; everything else stays unclassified rather than being
 * guessed from a name, and no name-derived icon suggests otherwise.
 */
@Component({
  selector: 'co-workflows-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon, ProjectMark, WorkflowDetail, WorkflowRow, WorkflowRunDialog],
  templateUrl: './workflows-page.html',
  styleUrl: './workflows-page.scss',
})
export class WorkflowsPage {
  private readonly store = inject(WorkflowsStore);

  protected readonly loadState = this.store.loadState;
  protected readonly isSampleData = this.store.isSampleData;
  protected readonly readAt = this.store.readAt;
  protected readonly totalCount = this.store.workflowCount;
  protected readonly jobsState = this.store.jobsState;
  protected readonly selectedJobs = this.store.selectedJobs;
  protected readonly selectedManualRun = this.store.selectedManualRun;
  protected readonly manualRunReading = this.store.manualRunReading;
  protected readonly savingRiskFor = this.store.savingRiskFor;
  protected readonly riskFailure = this.store.riskFailure;
  protected readonly dispatchStatus = this.store.dispatchStatus;
  protected readonly dispatchFailure = this.store.dispatchFailure;
  protected readonly awaitingRunFor = this.store.awaitingRunFor;
  protected readonly selectedInputs = this.store.selectedInputs;
  protected readonly branches = this.store.branches;
  protected readonly branchesState = this.store.branchesState;
  protected readonly branchesBounded = this.store.branchesBounded;

  /** The workflow a run is being asked for, or `null` when nothing is being asked. */
  protected readonly runTarget = signal<{ workflow: Workflow; defaultBranch: string } | null>(null);

  protected readonly search = signal('');
  protected readonly typeFilter = signal<TypeFilter>(null);
  protected readonly projectFilter = signal<string | null>(null);
  private readonly selectedId = signal<string | null>(null);

  constructor() {
    this.store.read();

    // A run in progress is what an operator watches, so the screen follows it instead of waiting to be
    // reloaded. While something is running it re-reads only the workflows that are running.
    // Closed only once the provider accepted. A refusal keeps the panel open, where the operator asked.
    effect(() => {
      if (this.store.dispatchStatus() === 'accepted') {
        untracked(() => this.runTarget.set(null));
      }
    });

    providerRefresh(
      () => this.store.refresh(),
      () =>
        this.store.hasRunningWorkflow()
          ? ACTIVE_PROVIDER_REFRESH_INTERVAL_MS
          : IDLE_PROVIDER_REFRESH_INTERVAL_MS,
    );
  }

  /** Projects to offer in the filter, from the inventory rather than from a separate list. */
  protected readonly projectOptions = computed(() =>
    this.store.groups().map((group) => ({ id: group.projectId, name: group.projectName })),
  );

  protected readonly groups = computed<readonly FilteredGroup[]>(() => {
    const term = this.search().trim().toLowerCase();
    const type = this.typeFilter();
    const project = this.projectFilter();

    return (
      this.store
        .groups()
        .filter((group) => project === null || group.projectId === project)
        .map((group) => ({
          projectId: group.projectId,
          projectName: group.projectName,
          readFailure: group.readFailure,
          workflows: group.workflows.filter((workflow) => {
            const matchesType = type === null || workflow.classification === type;
            const matchesTerm =
              term === '' ||
              workflow.name.toLowerCase().includes(term) ||
              workflow.path.toLowerCase().includes(term);

            return matchesType && matchesTerm;
          }),
        }))
        // A group that could not be read stays, because its failure is the fact worth showing.
        .filter((group) => group.workflows.length > 0 || group.readFailure !== null)
    );
  });

  /** How many workflows the filters are showing, so the page never implies it is showing all of them. */
  protected readonly shownCount = computed(() =>
    this.groups().reduce((total, group) => total + group.workflows.length, 0),
  );

  protected readonly isFiltered = computed(
    () =>
      this.search().trim() !== '' || this.typeFilter() !== null || this.projectFilter() !== null,
  );

  protected readonly selected = computed(() => {
    const id = this.selectedId();
    if (id === null) {
      return null;
    }

    for (const group of this.store.groups()) {
      const workflow = group.workflows.find((candidate) => candidate.id === id);
      if (workflow !== undefined) {
        return { workflow, projectId: group.projectId, projectName: group.projectName };
      }
    }

    return null;
  });

  protected toneFor(projectId: string): ProjectMarkTone {
    return toneForProject(projectId);
  }

  protected isSelected(workflow: Workflow): boolean {
    return this.selectedId() === workflow.id;
  }

  protected select(workflow: Workflow): void {
    this.selectedId.set(workflow.id);

    const projectId = this.projectOf(workflow.id);
    if (projectId === null) {
      this.store.clearRunJobs();
      return;
    }

    // Whether it can be dispatched is in the definition, so it is established for the selection rather than
    // asked for every workflow on the page.
    this.store.readManualRunSupport(projectId, workflow.id, workflow.path);

    // Jobs need a run. A workflow that has never run has nothing to read.
    const run = workflow.latestRun;
    if (run === null) {
      this.store.clearRunJobs();
      return;
    }

    this.store.readRunJobs(projectId, run.id);
  }

  private projectOf(workflowId: string): string | null {
    for (const group of this.store.groups()) {
      if (group.workflows.some((workflow) => workflow.id === workflowId)) {
        return group.projectId;
      }
    }

    return null;
  }

  protected setSearch(term: string): void {
    this.search.set(term);
  }

  protected setTypeFilter(filter: TypeFilter): void {
    this.typeFilter.set(filter);
  }

  protected setProjectFilter(projectId: string): void {
    this.projectFilter.set(projectId === '' ? null : projectId);
  }

  /** The one write on this screen: an operator saying how much intent running this workflow should require. */
  protected setRisk(workflow: Workflow, level: WorkflowRiskLevel): void {
    const projectId = this.projectOf(workflow.id);
    if (projectId !== null) {
      this.store.setRisk(projectId, workflow.path, level);
    }
  }

  /**
   * Opens the run panel for one workflow.
   *
   * Reads the definition first if it has not been read: the panel asks for what the workflow declares, and the
   * only way to know that is the definition.
   */
  protected requestRun(workflow: Workflow): void {
    const group = this.store
      .groups()
      .find((candidate) => candidate.workflows.some((item) => item.id === workflow.id));

    if (group === undefined) {
      return;
    }

    this.store.clearDispatch();
    this.store.readManualRunSupport(group.projectId, workflow.id, workflow.path);
    // Read the refs that exist, so the panel offers them rather than asking an operator to remember one.
    this.store.readBranches(group.projectId, group.defaultBranch);
    this.runTarget.set({ workflow, defaultBranch: group.defaultBranch });
  }

  protected cancelRun(): void {
    this.runTarget.set(null);
    this.store.clearDispatch();
  }

  protected confirmRun(request: {
    reference: string;
    inputs: Readonly<Record<string, string>>;
    confirmation: string | null;
  }): void {
    const target = this.runTarget();
    const projectId = target === null ? null : this.projectOf(target.workflow.id);
    if (target === null || projectId === null) {
      return;
    }

    // The panel stays open: a refusal belongs where the operator asked, not on a page behind it.
    this.store.dispatch(projectId, target.workflow, request);
  }

  protected clearFilters(): void {
    this.search.set('');
    this.typeFilter.set(null);
    this.projectFilter.set(null);
  }
}
