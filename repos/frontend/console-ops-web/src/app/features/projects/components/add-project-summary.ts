import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { EnvironmentKind } from '../../../core/contracts/dashboard-overview';
import { ProjectRegistrationRequest } from '../../../core/contracts/project-registration';
import { Icon, IconName } from '../../../core/ui/icon';

interface PreviewRow {
  readonly icon: IconName;
  readonly label: string;
  /** `null` renders as an explicit unavailable state so an incomplete form never looks complete. */
  readonly value: string | null;
  readonly environmentKind?: EnvironmentKind;
}

interface NextStep {
  readonly icon: IconName;
  readonly text: string;
}

/** What Console Ops does once a project is registered. Deterministic, not aspirational. */
const NEXT_STEPS: readonly NextStep[] = [
  { icon: 'stacks', text: 'The project is created with its first environment.' },
  { icon: 'github', text: 'An initial refresh reads the configured GitHub source and workflow.' },
  { icon: 'heartPulse', text: 'Health is probed when a health endpoint is configured.' },
  { icon: 'refresh', text: 'Version sync reports Unknown until a deployed commit is observed.' },
];

/**
 * Preview of the project Console Ops would create.
 *
 * It shows configuration only. No health, version, or sync result appears here, because none has been
 * observed yet - those arrive after registration, or from the pre-registration verification phase.
 */
@Component({
  selector: 'co-add-project-summary',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon],
  templateUrl: './add-project-summary.html',
  styleUrl: './add-project-summary.scss',
})
export class AddProjectSummary {
  readonly request = input.required<ProjectRegistrationRequest | null>();
  readonly environmentKind = input.required<EnvironmentKind>();

  protected readonly nextSteps = NEXT_STEPS;

  /** The preview stays empty until the required configuration composes a valid registration. */
  protected readonly hasPreview = computed(() => this.request() !== null);

  protected readonly rows = computed<readonly PreviewRow[]>(() => {
    const request = this.request();
    if (request === null) {
      return [];
    }

    const environment = request.environments[0];

    return [
      { icon: 'stacks', label: 'Project', value: request.name },
      {
        icon: 'github',
        label: 'Repository',
        value: `${request.repository.owner}/${request.repository.name}`,
      },
      { icon: 'ciCd', label: 'Source branch', value: request.repository.defaultBranch },
      {
        icon: 'ciCd',
        label: 'Deployment workflow',
        value: request.repository.workflowFile,
      },
      {
        icon: 'cloud',
        label: 'Environment',
        value: environment.name,
        environmentKind: this.environmentKind(),
      },
      { icon: 'codeWindow', label: 'Application', value: environment.applicationUrl },
      { icon: 'heartPulse', label: 'Health', value: environment.healthUrl },
      { icon: 'refresh', label: 'Version', value: environment.versionUrl },
    ];
  });
}
