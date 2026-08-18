import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, map, of, switchMap } from 'rxjs';

import { EnvironmentKind, StatusCell } from '../../core/contracts/dashboard-overview';
import { EndpointVerification } from '../../core/contracts/endpoint-verification';
import {
  DetectedEndpoint,
  GitHubLatestCommit,
  GitHubRepository,
  GitHubWorkflow,
} from '../../core/contracts/github-discovery';
import { ProjectRegistrationRequest } from '../../core/contracts/project-registration';
import { ProjectListItem } from '../../core/contracts/project-registry';
import { EndpointVerificationDataSource } from '../../core/data/endpoint-verification.data-source';
import { GitHubDiscoveryDataSource } from '../../core/data/github-discovery.data-source';
import { DashboardOverviewStore } from '../../core/state/dashboard-overview.store';
import { ProjectRegistryStore } from '../../core/state/project-registry.store';
import { Icon } from '../../core/ui/icon';
import { AddProjectSummary } from './components/add-project-summary';
import { EndpointMonitoring } from './components/endpoint-monitoring';
import { AzureLogSource } from '../../core/contracts/azure-discovery';
import { AzureLogSourcePicker } from './components/azure-log-source-picker';
import { GitHubRepositoryPicker } from './components/github-repository-picker';
import { RegistrationOutcomePanel } from './components/registration-outcome';
import { WorkflowSelector } from './components/workflow-selector';
import { toLogSource, validateOptionalLogSource } from './project-log-source-form';

interface Option<T> {
  readonly value: T;
  readonly label: string;
}

type SubmissionState = 'idle' | 'submitting' | 'failed';

/** The registered project, and whether the best-effort initial observation refresh ran. */
export interface RegistrationOutcome {
  readonly project: ProjectListItem;
  readonly refreshed: boolean;
}

const ENVIRONMENT_KINDS: readonly Option<EnvironmentKind>[] = [
  { value: 'production', label: 'Production' },
  { value: 'staging', label: 'Staging' },
  { value: 'development', label: 'Development' },
  { value: 'local', label: 'Local' },
];

/** Registers one project and its first environment through the frozen V1 project API. */
@Component({
  selector: 'co-add-project-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    AddProjectSummary,
    AzureLogSourcePicker,
    EndpointMonitoring,
    GitHubRepositoryPicker,
    Icon,
    RegistrationOutcomePanel,
    RouterLink,
    WorkflowSelector,
  ],
  templateUrl: './add-project-page.html',
  styleUrl: './add-project-page.scss',
})
export class AddProjectPage {
  private readonly projects = inject(ProjectRegistryStore);
  private readonly dashboard = inject(DashboardOverviewStore);
  private readonly discovery = inject(GitHubDiscoveryDataSource);
  private readonly endpointVerification = inject(EndpointVerificationDataSource);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly environmentKinds = ENVIRONMENT_KINDS;

  protected readonly name = signal('');
  protected readonly description = signal('');
  protected readonly repository = signal('');
  protected readonly defaultBranch = signal('main');
  protected readonly workflowFile = signal('');
  protected readonly environmentKind = signal<EnvironmentKind>('production');
  protected readonly environmentName = signal('Production');
  protected readonly environmentNameCustomized = signal(false);

  /** Repository chosen through discovery, or `null` while the operator types it manually. */
  protected readonly importedRepository = signal<GitHubRepository | null>(null);
  protected readonly pickerOpen = signal(false);
  protected readonly workflows = signal<readonly GitHubWorkflow[]>([]);
  protected readonly workflowsLoading = signal(false);
  protected readonly workflowsUnavailable = signal(false);

  /**
   * `false` until the operator picks a workflow or explicitly picks none, so the selector starts with
   * nothing selected rather than defaulting them into "no deployment workflow".
   */
  protected readonly workflowChosen = signal(false);

  /** `null` is the explicit "no deployment workflow" choice. */
  protected readonly chosenWorkflowFile = computed(() =>
    this.workflowFile().trim() === '' ? null : this.workflowFile().trim(),
  );

  protected readonly verification = signal<EndpointVerification | null>(null);
  protected readonly verifying = signal(false);
  protected readonly verificationError = signal<string | null>(null);

  /** Head commit of the imported branch, when discovery could read it. */
  protected readonly sourceCommit = signal<GitHubLatestCommit | null>(null);

  /** Paths recognised in repository source. Suggestions only, never applied automatically. */
  protected readonly detected = signal<readonly DetectedEndpoint[]>([]);

  protected readonly detectedHealth = computed(
    () => this.detected().find((endpoint) => endpoint.kind === 'health') ?? null,
  );

  protected readonly detectedVersion = computed(
    () => this.detected().find((endpoint) => endpoint.kind === 'version') ?? null,
  );

  /** A suggestion is only worth offering while the field does not already say the same thing. */
  protected readonly healthSuggestion = computed(() => {
    const suggestion = this.detectedHealth();
    return suggestion !== null && this.healthEndpoint().trim() !== suggestion.path
      ? suggestion
      : null;
  });

  protected readonly versionSuggestion = computed(() => {
    const suggestion = this.detectedVersion();
    return suggestion !== null && this.versionEndpoint().trim() !== suggestion.path
      ? suggestion
      : null;
  });

  /**
   * Source against deployed, stated only from two observed commits.
   *
   * Equal normalized SHAs are `In Sync`. Unequal SHAs are reported as differing without claiming a
   * direction, because ancestry is unknown until the project is registered and refreshed.
   */
  protected readonly sourceSync = computed<StatusCell | null>(() => {
    const source = this.sourceCommit();
    const deployed = this.verification()?.version.commitSha ?? null;

    if (source === null || deployed === null) {
      return null;
    }

    return source.commitSha.toLowerCase() === deployed.toLowerCase()
      ? { level: 'healthy', label: 'In Sync', detail: source.commitShortSha }
      : {
          level: 'warning',
          label: 'Differs',
          detail: 'Ancestry is known after the first refresh',
        };
  });

  /**
   * Verification is an explicit action, not something typing triggers.
   *
   * Probing on every keystroke would make the API contact arbitrary hosts as the operator types, waste
   * the endpoint's rate limit, and surprise them. It becomes available once there is a resolvable
   * endpoint to probe.
   */
  protected readonly canVerify = computed(
    () =>
      !this.verifying() &&
      this.baseUrlError() === null &&
      this.healthEndpointError() === null &&
      this.versionEndpointError() === null &&
      (resolveEndpoint(blankToNull(this.baseUrl()), this.healthEndpoint()) !== null ||
        resolveEndpoint(blankToNull(this.baseUrl()), this.versionEndpoint()) !== null),
  );
  protected readonly baseUrl = signal('');
  protected readonly healthEndpoint = signal('');
  protected readonly versionEndpoint = signal('');
  protected readonly submissionState = signal<SubmissionState>('idle');
  protected readonly submissionError = signal<string | null>(null);

  /**
   * What registration actually did, once it has. Held rather than redirected away from, so the operator
   * sees which steps completed instead of guessing from a list they land on.
   */
  protected readonly registered = signal<RegistrationOutcome | null>(null);

  protected readonly nameError = computed(() =>
    this.name().trim() === '' ? 'A project name is required.' : null,
  );

  protected readonly repositoryError = computed(() => {
    const value = this.repository().trim();
    if (value === '') return 'A repository is required.';
    return parseRepository(value) === null
      ? 'Use the form owner/name, such as clint/spinner.'
      : null;
  });

  protected readonly branchError = computed(() =>
    this.defaultBranch().trim() === '' ? 'A default branch is required.' : null,
  );

  protected readonly environmentNameError = computed(() =>
    this.environmentName().trim() === '' ? 'An environment name is required.' : null,
  );

  protected readonly baseUrlError = computed(() => validateHttpUrl(this.baseUrl().trim()));
  protected readonly healthEndpointError = computed(() =>
    validateEndpoint(this.healthEndpoint().trim(), this.baseUrl().trim()),
  );
  protected readonly versionEndpointError = computed(() =>
    validateEndpoint(this.versionEndpoint().trim(), this.baseUrl().trim()),
  );

  /** Optional: where this environment's container logs can be read from. */
  protected readonly logWorkspaceId = signal('');
  protected readonly logContainerAppName = signal('');
  protected readonly logSourceError = computed(() =>
    validateOptionalLogSource(this.logWorkspaceId(), this.logContainerAppName()),
  );

  /** Fills both fields from one Azure resource. They stay editable: discovery prefills, never decides. */
  protected applyLogSource(source: AzureLogSource): void {
    this.logContainerAppName.set(source.name);
    this.logWorkspaceId.set(source.workspaceId ?? '');
  }

  protected readonly isValid = computed(
    () =>
      this.nameError() === null &&
      this.repositoryError() === null &&
      this.branchError() === null &&
      this.environmentNameError() === null &&
      this.baseUrlError() === null &&
      this.healthEndpointError() === null &&
      this.versionEndpointError() === null &&
      this.logSourceError() === null,
  );

  protected readonly request = computed<ProjectRegistrationRequest | null>(() => {
    const repository = parseRepository(this.repository().trim());
    if (!this.isValid() || repository === null) return null;

    const applicationUrl = blankToNull(this.baseUrl());
    return {
      name: this.name().trim(),
      description: blankToNull(this.description()),
      repository: {
        ...repository,
        defaultBranch: this.defaultBranch().trim(),
        workflowFile: blankToNull(this.workflowFile()),
      },
      environments: [
        {
          name: this.environmentName().trim(),
          kind: this.environmentKind(),
          applicationUrl,
          healthUrl: resolveEndpoint(applicationUrl, this.healthEndpoint()),
          versionUrl: resolveEndpoint(applicationUrl, this.versionEndpoint()),
          logSource: toLogSource(this.logWorkspaceId(), this.logContainerAppName()),
        },
      ],
    };
  });

  protected selectEnvironmentKind(kind: EnvironmentKind): void {
    this.environmentKind.set(kind);

    // The name follows the kind until the operator takes it over, so choosing Production does not
    // also require typing "Production".
    if (!this.environmentNameCustomized()) {
      this.environmentName.set(labelFor(kind));
    }
  }

  /** Reveals the name field, after which the kind no longer overwrites it. */
  protected customizeEnvironmentName(): void {
    this.environmentNameCustomized.set(true);
  }

  protected openPicker(): void {
    this.pickerOpen.set(true);
  }

  protected closePicker(): void {
    this.pickerOpen.set(false);
  }

  /**
   * Takes what GitHub reported and stops asking for it.
   *
   * The project name is prefilled only while the operator has not typed one, because presentation
   * naming and repository naming legitimately differ.
   */
  protected importRepository(repository: GitHubRepository): void {
    this.importedRepository.set(repository);
    this.pickerOpen.set(false);
    this.repository.set(`${repository.owner}/${repository.name}`);
    this.defaultBranch.set(repository.defaultBranch);

    if (this.name().trim() === '') {
      this.name.set(repository.name);
    }

    this.loadWorkflows(repository);
    this.loadSourceCommit(repository);
  }

  /** Returns to manual entry and discards discovered facts, so nothing stale is submitted. */
  protected clearImportedRepository(): void {
    this.importedRepository.set(null);
    this.sourceCommit.set(null);
    this.detected.set([]);
    this.workflows.set([]);
    this.workflowsLoading.set(false);
    this.workflowsUnavailable.set(false);
    this.workflowFile.set('');
  }

  protected selectWorkflow(fileName: string | null): void {
    this.workflowFile.set(fileName ?? '');
    this.workflowChosen.set(true);
  }

  /** Asks the API to probe the configured endpoints and reports whatever it observed. */
  protected verifyEndpoints(): void {
    if (!this.canVerify()) {
      return;
    }

    const applicationUrl = blankToNull(this.baseUrl());
    this.verifying.set(true);
    this.verificationError.set(null);

    this.endpointVerification
      .verify({
        healthUrl: resolveEndpoint(applicationUrl, this.healthEndpoint()),
        versionUrl: resolveEndpoint(applicationUrl, this.versionEndpoint()),
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.verification.set(result);
          this.verifying.set(false);
        },
        error: (error: unknown) => {
          this.verification.set(null);
          this.verificationError.set(verificationErrorMessage(error));
          this.verifying.set(false);
        },
      });
  }

  /** A failure here is not fatal: the setup simply cannot compare source with deployed yet. */
  private loadSourceCommit(repository: GitHubRepository): void {
    this.sourceCommit.set(null);

    this.discovery
      .getLatestCommit(repository.owner, repository.name, repository.defaultBranch)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (commit) => this.sourceCommit.set(commit),
        error: () => this.sourceCommit.set(null),
      });

    this.detected.set([]);
    this.discovery
      .detectEndpoints(repository.owner, repository.name, repository.defaultBranch)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => this.detected.set(result.endpoints),
        // Detection is optional: without it the operator simply types the paths.
        error: () => this.detected.set([]),
      });
  }

  /** Applying a suggestion is the operator's decision; detection never fills a field on its own. */
  protected applyHealthSuggestion(): void {
    const suggestion = this.detectedHealth();
    if (suggestion !== null) {
      this.healthEndpoint.set(suggestion.path);
    }
  }

  protected applyVersionSuggestion(): void {
    const suggestion = this.detectedVersion();
    if (suggestion !== null) {
      this.versionEndpoint.set(suggestion.path);
    }
  }

  private loadWorkflows(repository: GitHubRepository): void {
    this.workflows.set([]);
    this.workflowChosen.set(false);
    this.workflowsUnavailable.set(false);
    this.workflowsLoading.set(true);

    this.discovery
      .listWorkflows(repository.owner, repository.name)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.workflows.set(result.workflows);
          this.workflowsLoading.set(false);
        },
        // Discovery is optional: fall back to naming the workflow file by hand.
        error: () => {
          this.workflowsUnavailable.set(true);
          this.workflowsLoading.set(false);
        },
      });
  }

  protected submit(): void {
    const request = this.request();
    if (request === null || this.submissionState() === 'submitting') return;

    this.submissionState.set('submitting');
    this.submissionError.set(null);

    this.projects
      .register(request)
      .pipe(
        switchMap((project) =>
          this.projects.refreshProject(project.id).pipe(
            // Registration is durable even if the best-effort initial observation cannot run, so the
            // outcome records whether it ran rather than hiding the failure.
            map(() => ({ project, refreshed: true })),
            catchError(() => of({ project, refreshed: false })),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: ({ project, refreshed }) => {
          this.dashboard.refresh();
          this.submissionState.set('idle');
          this.registered.set({ project, refreshed });
        },
        error: (error: unknown) => {
          this.submissionState.set('failed');
          this.submissionError.set(registrationErrorMessage(error));
        },
      });
  }
}

function labelFor(kind: EnvironmentKind): string {
  return ENVIRONMENT_KINDS.find((option) => option.value === kind)?.label ?? 'Environment';
}

function blankToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === '' ? null : trimmed;
}

function parseRepository(value: string): { owner: string; name: string } | null {
  const match = /^([\w.-]+)\/([\w.-]+)$/.exec(value);
  return match === null ? null : { owner: match[1], name: match[2] };
}

/** V1 rule: absolute HTTP(S) URL with no embedded credentials. */
function validateHttpUrl(value: string): string | null {
  if (value === '') return null;

  let url: URL;
  try {
    url = new URL(value);
  } catch {
    return 'Enter an absolute URL, such as https://api.example.com.';
  }

  if (url.protocol !== 'http:' && url.protocol !== 'https:') {
    return 'Only http and https URLs are supported.';
  }

  if (url.username !== '' || url.password !== '') {
    return 'Remove the credentials from the URL. Console Ops never stores them.';
  }

  return null;
}

function validateEndpoint(value: string, baseUrl: string): string | null {
  if (value === '') return null;

  if (value.startsWith('/')) {
    return baseUrl === '' ? 'Add a Base URL before using a relative endpoint.' : null;
  }

  return validateHttpUrl(value) === null ? null : 'Use a path such as /health, or an absolute URL.';
}

function resolveEndpoint(baseUrl: string | null, endpoint: string): string | null {
  const value = endpoint.trim();
  if (value === '') return null;
  if (!value.startsWith('/')) return value;
  return new URL(value, baseUrl!).toString();
}

/**
 * A failed check is about the check, never about the configuration.
 *
 * An unreachable application comes back as a successful observation, so reaching here means Console Ops
 * itself could not run the probe.
 */
function verificationErrorMessage(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return 'The endpoints could not be checked. Try again.';
  }

  if (error.status === 0) {
    return 'The Console Ops API is unavailable, so the endpoints could not be checked.';
  }
  if (error.status === 429) {
    return 'Too many checks in a short time. Wait a moment and try again.';
  }
  if (error.status === 400) {
    return 'The endpoints could not be checked. Review the URLs above.';
  }
  return 'The endpoints could not be checked. Try again.';
}

function registrationErrorMessage(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return 'The project could not be added. Please try again.';
  }

  if (error.status === 0) return 'The Console Ops API is unavailable. Start the API and try again.';
  if (error.status === 409) return 'A project with that name or repository is already registered.';
  if (error.status === 400)
    return 'The API rejected this configuration. Review the fields and try again.';
  return 'The project could not be added. Please try again.';
}
