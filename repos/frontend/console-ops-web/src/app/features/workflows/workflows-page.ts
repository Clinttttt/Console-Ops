import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';

import { Workflow, WorkflowClassification } from '../../core/contracts/workflows';
import { WorkflowsStore } from '../../core/state/workflows.store';
import { Icon } from '../../core/ui/icon';
import { ProjectMark, ProjectMarkTone } from '../../core/ui/project-mark';
import { toneForProject } from '../../core/ui/project-tone';
import { WorkflowDetail } from './components/workflow-detail';
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
  imports: [Icon, ProjectMark, WorkflowDetail, WorkflowRow],
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

  protected readonly search = signal('');
  protected readonly typeFilter = signal<TypeFilter>(null);
  protected readonly projectFilter = signal<string | null>(null);
  private readonly selectedId = signal<string | null>(null);

  constructor() {
    this.store.read();
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

    // Jobs are read for the selection only. A workflow with no run has nothing to read.
    const run = workflow.latestRun;
    const projectId = this.projectOf(workflow.id);
    if (run === null || projectId === null) {
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

  protected clearFilters(): void {
    this.search.set('');
    this.typeFilter.set(null);
    this.projectFilter.set(null);
  }
}
