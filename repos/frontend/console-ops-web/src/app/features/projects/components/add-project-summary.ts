import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { EnvironmentKind } from '../../../core/contracts/dashboard-overview';
import { ProjectRegistrationRequest } from '../../../core/contracts/project-registration';
import { Icon, IconName } from '../../../core/ui/icon';

interface SummaryRow {
  readonly icon: IconName;
  readonly label: string;
  /** `null` renders as "Not set" so an incomplete form never looks complete. */
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

/** Live review of the registration the form would send. */
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

  protected readonly rows = computed<readonly SummaryRow[]>(() => {
    const request = this.request();
    const environment = request?.environments[0] ?? null;

    return [
      { icon: 'stacks', label: 'Project', value: request?.name ?? null },
      {
        icon: 'github',
        label: 'Repository',
        value:
          request === null
            ? null
            : `${request.repository.owner}/${request.repository.name} (${request.repository.defaultBranch})`,
      },
      {
        icon: 'cloud',
        label: 'Environment',
        value: environment?.name ?? null,
        environmentKind: this.environmentKind(),
      },
      {
        icon: 'ciCd',
        label: 'Workflow',
        value: request?.repository.workflowFile ?? null,
      },
      { icon: 'codeWindow', label: 'Base URL', value: environment?.applicationUrl ?? null },
      { icon: 'heartPulse', label: 'Health Endpoint', value: environment?.healthUrl ?? null },
      { icon: 'refresh', label: 'Version Endpoint', value: environment?.versionUrl ?? null },
    ];
  });
}
