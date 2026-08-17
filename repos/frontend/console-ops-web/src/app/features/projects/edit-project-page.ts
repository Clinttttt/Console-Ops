import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { rxResource, takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { map } from 'rxjs';

import { EnvironmentKind } from '../../core/contracts/dashboard-overview';
import { ProjectListItem } from '../../core/contracts/project-registry';
import {
  ProjectEnvironmentUpdate,
  ProjectUpdateRequest,
} from '../../core/contracts/project-update';
import { AzureLogSource } from '../../core/contracts/azure-discovery';
import { ProjectRegistryDataSource } from '../../core/data/project-registry.data-source';
import { ProjectRegistryStore } from '../../core/state/project-registry.store';
import { AzureLogSourcePicker } from './components/azure-log-source-picker';
import { toLogSource, validateOptionalLogSource } from './project-log-source-form';
import { Icon } from '../../core/ui/icon';

type LoadState = 'loading' | 'loaded' | 'notFound' | 'unavailable';
type SaveState = 'idle' | 'saving' | 'failed';

/**
 * One environment being edited.
 *
 * `id` is `null` for an environment that does not exist yet: the API creates an entry sent without one,
 * and matches an entry that carries one. `key` is a client-side identity so the list can be tracked and
 * reordered without depending on a server id that may not exist.
 */
interface EnvironmentDraft {
  readonly key: string;
  readonly id: string | null;
  name: string;
  kind: EnvironmentKind;
  applicationUrl: string;
  healthUrl: string;
  versionUrl: string;
  /** Log Analytics workspace holding this environment's container logs. Blank when not configured. */
  logWorkspaceId: string;
  /** Container app whose console output belongs to this environment. */
  logContainerAppName: string;
}

const ENVIRONMENT_KINDS: readonly { value: EnvironmentKind; label: string }[] = [
  { value: 'production', label: 'Production' },
  { value: 'staging', label: 'Staging' },
  { value: 'development', label: 'Development' },
  { value: 'local', label: 'Local' },
];

/**
 * Edits the registered configuration of one project.
 *
 * `PUT /api/projects/{id}` replaces the editable configuration, so the form always sends the complete
 * repository and environment list, and carries the `configurationVersion` it loaded. A stale version is
 * rejected by the API rather than overwriting someone else's change, and that rejection is reported
 * plainly instead of being retried.
 */
@Component({
  selector: 'co-edit-project-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [AzureLogSourcePicker, Icon, RouterLink],
  templateUrl: './edit-project-page.html',
  styleUrl: './edit-project-page.scss',
})
export class EditProjectPage {
  private readonly projects = inject(ProjectRegistryDataSource);
  private readonly store = inject(ProjectRegistryStore);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly environmentKinds = ENVIRONMENT_KINDS;

  protected readonly projectId = toSignal(
    inject(ActivatedRoute).paramMap.pipe(map((params) => params.get('projectId') ?? '')),
    { initialValue: '' },
  );

  private readonly resource = rxResource({
    params: () => this.projectId() || undefined,
    stream: ({ params }) => this.projects.getProject(params),
  });

  protected readonly loadState = computed<LoadState>(() => {
    if (this.resource.isLoading()) {
      return 'loading';
    }

    const error = this.resource.error();
    if (error !== undefined) {
      return error instanceof HttpErrorResponse && error.status === 404
        ? 'notFound'
        : 'unavailable';
    }

    return this.resource.value() === undefined ? 'loading' : 'loaded';
  });

  protected readonly name = signal('');
  protected readonly description = signal('');
  protected readonly repositoryOwner = signal('');
  protected readonly repositoryName = signal('');
  protected readonly defaultBranch = signal('');
  protected readonly workflowFile = signal('');
  protected readonly environments = signal<readonly EnvironmentDraft[]>([]);

  private readonly loadedVersion = signal<number | null>(null);

  protected readonly saveState = signal<SaveState>('idle');
  protected readonly saveError = signal<string | null>(null);
  protected readonly archiveConfirming = signal(false);
  protected readonly archiving = signal(false);

  protected readonly nameError = computed(() =>
    this.name().trim() === '' ? 'A project name is required.' : null,
  );

  protected readonly repositoryError = computed(() =>
    this.repositoryOwner().trim() === '' || this.repositoryName().trim() === ''
      ? 'A repository owner and name are required.'
      : null,
  );

  protected readonly branchError = computed(() =>
    this.defaultBranch().trim() === '' ? 'A default branch is required.' : null,
  );

  protected readonly environmentErrors = computed(() => {
    const environments = this.environments();
    const nameCounts = new Map<string, number>();
    for (const environment of environments) {
      const key = environment.name.trim().toLowerCase();
      nameCounts.set(key, (nameCounts.get(key) ?? 0) + 1);
    }

    return environments.map((environment) => ({
      name: environmentNameError(environment.name, nameCounts),
      applicationUrl: validateOptionalHttpUrl(environment.applicationUrl),
      healthUrl: validateOptionalHttpUrl(environment.healthUrl),
      versionUrl: validateOptionalHttpUrl(environment.versionUrl),
      logSource: validateOptionalLogSource(
        environment.logWorkspaceId,
        environment.logContainerAppName,
      ),
    }));
  });

  /** Key of the environment whose removal is awaiting confirmation. */
  protected readonly removingKey = signal<string | null>(null);

  /** Fills both fields from one Azure resource. They stay editable: discovery prefills, never decides. */
  protected applyLogSource(index: number, source: AzureLogSource): void {
    this.updateEnvironment(index, {
      logContainerAppName: source.name,
      logWorkspaceId: source.workspaceId ?? '',
    });
  }

  /** V1 requires every project to keep at least one environment. */
  protected readonly canRemoveEnvironment = computed(() => this.environments().length > 1);

  protected readonly isValid = computed(
    () =>
      this.nameError() === null &&
      this.repositoryError() === null &&
      this.branchError() === null &&
      this.environmentErrors().every(
        (errors) =>
          errors.name === null &&
          errors.applicationUrl === null &&
          errors.healthUrl === null &&
          errors.versionUrl === null &&
          errors.logSource === null,
      ),
  );

  constructor() {
    // Fills the form once the project loads, and again if the route points at another project.
    effect(() => {
      const project = this.resource.value();
      if (project !== undefined) {
        this.applyLoaded(project);
      }
    });
  }

  protected updateEnvironment(index: number, patch: Partial<EnvironmentDraft>): void {
    this.environments.update((environments) =>
      environments.map((environment, position) =>
        position === index ? { ...environment, ...patch } : environment,
      ),
    );
  }

  protected addEnvironment(): void {
    this.environments.update((environments) => [
      ...environments,
      {
        key: `new-${environments.length}-${Date.now()}`,
        id: null,
        name: '',
        kind: 'staging',
        applicationUrl: '',
        healthUrl: '',
        versionUrl: '',
        logWorkspaceId: '',
        logContainerAppName: '',
      },
    ]);
  }

  /**
   * Asks before removing an environment that exists, because removing it discards its observations.
   * A draft that was never saved has nothing to lose, so it goes immediately.
   */
  protected requestRemoveEnvironment(draft: EnvironmentDraft): void {
    if (draft.id === null) {
      this.removeEnvironment(draft.key);
      return;
    }

    this.removingKey.set(draft.key);
  }

  protected cancelRemoveEnvironment(): void {
    this.removingKey.set(null);
  }

  protected removeEnvironment(key: string): void {
    if (!this.canRemoveEnvironment()) {
      return;
    }

    this.environments.update((environments) =>
      environments.filter((environment) => environment.key !== key),
    );
    this.removingKey.set(null);
  }

  protected save(): void {
    const version = this.loadedVersion();
    if (!this.isValid() || version === null || this.saveState() === 'saving') {
      return;
    }

    this.saveState.set('saving');
    this.saveError.set(null);

    this.store
      .updateProject(this.projectId(), this.buildRequest(version))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.saveState.set('idle');
          void this.router.navigate(['/projects', this.projectId()]);
        },
        error: (error: unknown) => {
          this.saveState.set('failed');
          this.saveError.set(saveErrorMessage(error));
        },
      });
  }

  protected confirmArchive(): void {
    this.archiveConfirming.set(true);
  }

  protected cancelArchive(): void {
    this.archiveConfirming.set(false);
  }

  protected archive(): void {
    if (this.archiving()) {
      return;
    }

    this.archiving.set(true);
    this.saveError.set(null);

    this.store
      .archiveProject(this.projectId())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.archiving.set(false);
          void this.router.navigateByUrl('/projects');
        },
        error: (error: unknown) => {
          this.archiving.set(false);
          this.archiveConfirming.set(false);
          this.saveError.set(archiveErrorMessage(error));
        },
      });
  }

  private applyLoaded(project: ProjectListItem): void {
    this.name.set(project.name);
    this.description.set(project.description ?? '');
    this.repositoryOwner.set(project.repository.owner);
    this.repositoryName.set(project.repository.name);
    this.defaultBranch.set(project.repository.defaultBranch);
    this.workflowFile.set(project.repository.workflowFile ?? '');
    this.loadedVersion.set(project.configurationVersion);
    this.environments.set(
      project.environments.map((environment) => ({
        key: environment.id,
        id: environment.id,
        name: environment.name,
        kind: environment.kind,
        applicationUrl: environment.applicationUrl ?? '',
        healthUrl: environment.healthUrl ?? '',
        versionUrl: environment.versionUrl ?? '',
        logWorkspaceId: environment.logSource?.workspaceId ?? '',
        logContainerAppName: environment.logSource?.containerAppName ?? '',
      })),
    );
    this.removingKey.set(null);
  }

  private buildRequest(configurationVersion: number): ProjectUpdateRequest {
    const environments: ProjectEnvironmentUpdate[] = this.environments().map((environment) => ({
      // A new environment omits the id entirely; the API creates it.
      ...(environment.id === null ? {} : { id: environment.id }),
      name: environment.name.trim(),
      kind: environment.kind,
      applicationUrl: blankToNull(environment.applicationUrl),
      healthUrl: blankToNull(environment.healthUrl),
      versionUrl: blankToNull(environment.versionUrl),
      logSource: toLogSource(environment.logWorkspaceId, environment.logContainerAppName),
    }));

    return {
      configurationVersion,
      name: this.name().trim(),
      description: blankToNull(this.description()),
      repository: {
        owner: this.repositoryOwner().trim(),
        name: this.repositoryName().trim(),
        defaultBranch: this.defaultBranch().trim(),
        workflowFile: blankToNull(this.workflowFile()),
      },
      environments,
    };
  }
}

function blankToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === '' ? null : trimmed;
}

/** Names must exist and be unique within a project, as the V1 contract requires. */
function environmentNameError(name: string, counts: Map<string, number>): string | null {
  const trimmed = name.trim();
  if (trimmed === '') {
    return 'An environment name is required.';
  }

  return (counts.get(trimmed.toLowerCase()) ?? 0) > 1
    ? 'Environment names must be unique within a project.'
    : null;
}

/** V1 rule: absolute HTTP(S) URL with no embedded credentials. */
function validateOptionalHttpUrl(value: string): string | null {
  const trimmed = value.trim();
  if (trimmed === '') {
    return null;
  }

  let url: URL;
  try {
    url = new URL(trimmed);
  } catch {
    return 'Enter an absolute URL, such as https://api.example.com/health.';
  }

  if (url.protocol !== 'http:' && url.protocol !== 'https:') {
    return 'Only http and https URLs are supported.';
  }

  return url.username === '' && url.password === ''
    ? null
    : 'Remove the credentials from the URL. Console Ops never stores them.';
}

function saveErrorMessage(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return 'The changes could not be saved. Try again.';
  }

  if (error.status === 0) return 'The Console Ops API is unavailable, so nothing was saved.';
  if (error.status === 409) {
    return 'This project changed since you opened it, so nothing was saved. Reload and reapply your changes.';
  }
  if (error.status === 400) return 'The API rejected this configuration. Review the fields above.';
  return 'The changes could not be saved. Try again.';
}

function archiveErrorMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse && error.status === 0) {
    return 'The Console Ops API is unavailable, so the project was not archived.';
  }
  return 'The project could not be archived. Try again.';
}
