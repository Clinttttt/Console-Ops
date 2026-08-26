import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { SignInRefusal } from '../../core/contracts/session';
import { Icon } from '../../core/ui/icon';

/**
 * Sign in with GitHub.
 *
 * The only screen that answers before a session exists. It explains a refusal rather than repeating that something
 * went wrong: "that account is not an operator here" and "GitHub could not be reached" ask for different things, and
 * an operator who cannot tell them apart will retry the one that cannot work.
 */
@Component({
  selector: 'co-sign-in-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon],
  templateUrl: './sign-in-page.html',
  styleUrl: './sign-in-page.scss',
})
export class SignInPage {
  /** Bound from the query string, which is where the callback reports a refusal. */
  readonly error = input<string | null>(null);

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
        return reason;
      default:
        // An unrecognised code is still shown as a refusal, because it is one, but not repeated back verbatim.
        return 'unknown';
    }
  });
}
