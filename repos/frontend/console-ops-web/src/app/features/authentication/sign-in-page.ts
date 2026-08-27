import { HttpClient } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  untracked,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';

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
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  /** Bound from the query string, which is where the callback reports a refusal. */
  readonly error = input<string | null>(null);

  /** Where the operator was going when the API could not be asked who they were. */
  readonly returnTo = input<string | null>(null);

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

    // An operator who was already signed in and whose API was merely asleep should not be asked to sign in again.
    // Once it answers, this asks whether the session survived and, if it did, puts them back where they were going.
    effect(() => {
      if (this.readiness() !== 'ready' || untracked(() => this.error()) !== 'unreachable') {
        return;
      }

      untracked(() => this.resumeIfStillSignedIn());
    });
  }

  protected retry(): void {
    this.api.probe();
  }

  private resumeIfStillSignedIn(): void {
    this.http
      .get<unknown>('/api/auth/session')
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => void this.router.navigateByUrl(this.returnTo() ?? '/overview'),
        // Nobody is signed in, or nothing answered again. The page already says so and offers GitHub.
        error: () => undefined,
      });
  }
}
