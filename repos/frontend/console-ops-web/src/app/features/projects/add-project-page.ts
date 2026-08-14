import { ChangeDetectionStrategy, Component, computed, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { EnvironmentKind } from '../../core/contracts/dashboard-overview';
import { ProjectRegistrationRequest } from '../../core/contracts/project-registration';
import { ProjectKind } from '../../core/contracts/project-registry';
import { Icon } from '../../core/ui/icon';
import { Toggle } from '../../core/ui/toggle';
import { AddProjectSummary } from './components/add-project-summary';

interface Option<T> {
  readonly value: T;
  readonly label: string;
}

const PROJECT_KINDS: readonly Option<ProjectKind>[] = [
  { value: 'api', label: 'API' },
  { value: 'webApp', label: 'Web App' },
  { value: 'worker', label: 'Worker' },
];

const ENVIRONMENT_KINDS: readonly Option<EnvironmentKind>[] = [
  { value: 'production', label: 'Production' },
  { value: 'staging', label: 'Staging' },
  { value: 'development', label: 'Development' },
  { value: 'local', label: 'Local' },
];

/** Hosting targets Console Ops can describe today. Free text is not accepted for a monitored target. */
const RUNTIME_TARGETS: readonly string[] = [
  'Azure Container Apps',
  'Azure App Service',
  'Azure Virtual Machine',
  'Docker Desktop',
  'Local process',
];

/**
 * Add Project screen: registers one application surface and its primary environment.
 *
 * Design stage. The form validates against the frozen V1 registration rules and composes a typed
 * `ProjectRegistrationRequest`, but nothing is submitted until the register-project slice is wired,
 * so the submit control stays explicitly unavailable rather than silently doing nothing.
 */
@Component({
  selector: 'co-add-project-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [AddProjectSummary, Icon, RouterLink, Toggle],
  templateUrl: './add-project-page.html',
  styleUrl: './add-project-page.scss',
})
export class AddProjectPage {
  protected readonly projectKinds = PROJECT_KINDS;
  protected readonly environmentKinds = ENVIRONMENT_KINDS;
  protected readonly runtimeTargets = RUNTIME_TARGETS;

  protected readonly name = signal('');
  protected readonly description = signal('');
  protected readonly kind = signal<ProjectKind | null>(null);
  protected readonly repository = signal('');
  protected readonly defaultBranch = signal('main');
  protected readonly environmentKind = signal<EnvironmentKind>('production');
  protected readonly environmentName = signal('Production');
  protected readonly runtimeTarget = signal('');
  protected readonly baseUrl = signal('');
  protected readonly healthEndpoint = signal('/health');
  protected readonly versionEndpoint = signal('/version');
  protected readonly healthMonitoring = signal(true);
  protected readonly versionSync = signal(true);

  /** Field-level messages, shown only once the operator has entered something. */
  protected readonly nameError = computed(() =>
    this.name().trim() === '' ? 'A project name is required.' : null,
  );

  protected readonly repositoryError = computed(() => {
    const value = this.repository().trim();
    if (value === '') {
      return 'A repository is required.';
    }
    return parseRepository(value) === null
      ? 'Use the form owner/name, such as clint/spinner.'
      : null;
  });

  protected readonly branchError = computed(() =>
    this.defaultBranch().trim() === '' ? 'A default branch is required.' : null,
  );

  protected readonly baseUrlError = computed(() => validateBaseUrl(this.baseUrl().trim()));

  protected readonly healthEndpointError = computed(() =>
    validateEndpoint(this.healthEndpoint().trim()),
  );

  protected readonly versionEndpointError = computed(() =>
    validateEndpoint(this.versionEndpoint().trim()),
  );

  protected readonly isValid = computed(
    () =>
      this.nameError() === null &&
      this.repositoryError() === null &&
      this.branchError() === null &&
      this.baseUrlError() === null &&
      this.healthEndpointError() === null &&
      this.versionEndpointError() === null,
  );

  /**
   * The request this screen would send. Kept typed so the form and the API contract cannot drift,
   * and `null` while the input is incomplete.
   */
  protected readonly request = computed<ProjectRegistrationRequest | null>(() => {
    const repository = parseRepository(this.repository().trim());
    if (!this.isValid() || repository === null) {
      return null;
    }

    return {
      name: this.name().trim(),
      description: blankToNull(this.description()),
      kind: this.kind(),
      repository: { ...repository, defaultBranch: this.defaultBranch().trim() },
      environments: [
        {
          name: this.environmentName().trim() || labelFor(this.environmentKind()),
          kind: this.environmentKind(),
          applicationUrl: blankToNull(this.baseUrl()),
          healthUrl: blankToNull(this.healthEndpoint()),
          versionUrl: blankToNull(this.versionEndpoint()),
        },
      ],
      runtime: { target: blankToNull(this.runtimeTarget()) },
      monitoring: {
        healthMonitoring: this.healthMonitoring(),
        versionSync: this.versionSync(),
      },
    };
  });

  protected selectEnvironmentKind(kind: EnvironmentKind): void {
    const previousLabel = labelFor(this.environmentKind());
    this.environmentKind.set(kind);

    // Keep the environment name in step unless the operator renamed it themselves.
    if (this.environmentName().trim() === previousLabel) {
      this.environmentName.set(labelFor(kind));
    }
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
function validateBaseUrl(value: string): string | null {
  if (value === '') {
    return null;
  }

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

function validateEndpoint(value: string): string | null {
  if (value === '') {
    return null;
  }

  if (value.startsWith('/')) {
    return null;
  }

  return validateBaseUrl(value) === null ? null : 'Use a path such as /health, or an absolute URL.';
}
