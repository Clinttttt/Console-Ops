import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';

import { SignInRefusal } from '../../core/contracts/session';
import { ApiReadiness } from '../../core/state/api-readiness';
import { Icon } from '../../core/ui/icon';

/**
 * Sign in with GitHub.
 *
 * The only screen that answers before a session exists, and now the only place an operator without one lands. It
 * explains a refusal rather than repeating that something went wrong: "that account is not an operator here" and
 * "GitHub could not be reached" ask for different things, and an operator who cannot tell them apart will retry the
 * one that cannot work.
 *
 * The action waits for the API before it is offered, because the deployment scales to zero and a redirect to GitHub
 * that returns to a cold API strands the operator on a proxy error page.
 */
@Component({
  selector: 'co-sign-in-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon],
  templateUrl: './sign-in-page.html',
  styleUrl: './sign-in-page.scss',
})
export class SignInPage {
  private readonly api = inject(ApiReadiness);

  /** Bound from the query string, which is where the callback reports a refusal. */
  readonly error = input<string | null>(null);

  protected readonly readiness = this.api.readiness;

  protected readonly refusal = computed<SignInRefusal | null>(() => {
    const reason = this.error();
    if (reason === null || reason.trim() === '') {
      return null;
    }

    switch (reason) {
      case 'state':
      case 'declined':
      case 'Auth.NotAnOperator':
      case 'Auth.NoOperatorsConfigured':
      case 'Auth.CodeRejected':
      case 'Auth.ProviderUnavailable':
      case 'unavailable':
      case 'unreachable':
        return reason;
      default:
        // An unrecognised code is still shown as a refusal, because it is one, but not repeated back verbatim.
        return 'unknown';
    }
  });

  constructor() {
    this.api.probe();
  }

  protected retry(): void {
    this.api.probe();
  }
}
