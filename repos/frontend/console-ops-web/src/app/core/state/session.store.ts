import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';

import { OperatorSession } from '../contracts/session';

export type SessionState = 'unknown' | 'signedIn' | 'signedOut' | 'unavailable';

/**
 * Who is signed in.
 *
 * Read from the API rather than assumed, because a session can end while a tab is open: a token that cannot be
 * renewed, or an operator removed from the allow list. The screen asks, and shows what it is told.
 */
@Injectable({ providedIn: 'root' })
export class SessionStore {
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);

  private readonly current = signal<OperatorSession | null>(null);
  private readonly state = signal<SessionState>('unknown');

  readonly loadState = this.state.asReadonly();
  readonly operator = this.current.asReadonly();
  readonly isSignedIn = computed(() => this.state() === 'signedIn');

  /**
   * Whether the API is reachable but has no session for this browser.
   *
   * Told apart from an unreachable API on purpose: one asks an operator to sign in, and the other would be asking
   * them to fix a problem that is not theirs.
   */
  readonly isSignedOut = computed(() => this.state() === 'signedOut');

  read(): void {
    this.http
      .get<OperatorSession>('/api/auth/session')
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (session) => {
          this.current.set(session);
          this.state.set('signedIn');
        },
        error: (error: unknown) => {
          this.current.set(null);
          // 403 is the API saying nobody is signed in. Anything else is the API being unreachable, which is a
          // different thing to tell an operator.
          this.state.set(
            error instanceof HttpErrorResponse && (error.status === 403 || error.status === 401)
              ? 'signedOut'
              : 'unavailable',
          );
        },
      });
  }

  /** Ends the session on the server, then sends the browser to the sign-in screen. */
  signOut(): void {
    this.http
      .post<void>('/api/auth/sign-out', {})
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.afterSignOut(),
        // Whatever the server said, this browser is done with the session it was holding.
        error: () => this.afterSignOut(),
      });
  }

  private afterSignOut(): void {
    this.current.set(null);
    this.state.set('signedOut');
    // Through the router rather than the window: the store should not know it is in a browser, and a navigation is
    // testable where a location assignment is not.
    void this.router.navigateByUrl('/sign-in');
  }
}
