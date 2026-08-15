import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';

import { EndpointVerification } from '../../../core/contracts/endpoint-verification';
import { Icon, IconName } from '../../../core/ui/icon';
import { RegistrationOutcome } from '../add-project-page';

/** A step that completed, did not run, or is waiting on something later. */
type StepState = 'done' | 'pending' | 'skipped';

interface CompletedStep {
  readonly icon: IconName;
  readonly label: string;
  readonly detail: string;
  readonly state: StepState;
}

/**
 * What registration actually did.
 *
 * Every line reports a completed step or says plainly that it did not happen. Nothing is listed
 * optimistically: a probe that was never run is reported as waiting for the next refresh, not as passing.
 */
@Component({
  selector: 'co-registration-outcome',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon, RouterLink],
  templateUrl: './registration-outcome.html',
  styleUrl: './registration-outcome.scss',
})
export class RegistrationOutcomePanel {
  readonly outcome = input.required<RegistrationOutcome>();
  /** Present only when the operator checked the endpoints before registering. */
  readonly verification = input<EndpointVerification | null>(null);

  protected readonly project = computed(() => this.outcome().project);

  protected readonly steps = computed<readonly CompletedStep[]>(() => {
    const project = this.project();
    const environment = project.environments[0];
    const checked = this.verification();
    const steps: CompletedStep[] = [
      {
        icon: 'stacks',
        label: 'Project registered',
        detail: project.name,
        state: 'done',
      },
      {
        icon: 'cloud',
        label: 'Environment created',
        detail: environment === undefined ? 'None' : `${environment.name} (${environment.kind})`,
        state: environment === undefined ? 'skipped' : 'done',
      },
      {
        icon: 'github',
        label: 'Source connected',
        detail: `${project.repository.owner}/${project.repository.name} · ${project.repository.defaultBranch}`,
        state: 'done',
      },
      {
        icon: 'ciCd',
        label: 'Deployment workflow',
        detail: project.repository.workflowFile ?? 'Not configured, so CI stays notConfigured',
        state: project.repository.workflowFile === null ? 'skipped' : 'done',
      },
    ];

    steps.push({
      icon: 'refresh',
      label: 'Initial observation',
      detail: this.outcome().refreshed
        ? 'Source, CI and endpoints were read'
        : 'Could not run now; the next refresh will read them',
      state: this.outcome().refreshed ? 'done' : 'pending',
    });

    if (environment?.healthUrl != null) {
      steps.push({
        icon: 'heartPulse',
        label: 'Health endpoint',
        detail:
          checked === null
            ? 'Configured; it is probed on each refresh'
            : `Checked before registering: ${checked.health.state}`,
        state: checked === null ? 'pending' : 'done',
      });
    }

    return steps;
  });
}
