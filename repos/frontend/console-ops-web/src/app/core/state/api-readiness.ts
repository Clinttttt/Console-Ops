import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, of, retry, throwError, timeout, timer } from 'rxjs';

export type ApiReadinessState = 'checking' | 'ready' | 'unreachable';

/** Each attempt is bounded: the proxy holds a request open while a container starts rather than refusing it. */
const ATTEMPT_TIMEOUT_MS = 8000;

/** Roughly forty seconds of patience in total, against a cold start measured at twenty-five. */
const ATTEMPTS_AFTER_THE_FIRST = 4;
const DELAY_BETWEEN_ATTEMPTS_MS = 2000;

/**
 * Whether the API is answering yet.
 *
 * A deployment can be asleep or restarting, and the proxy in front of it answers `502` rather than waiting - "not
 * awake", not "broken". Sending an operator to GitHub before then produces exactly that, on a page they cannot get
 * back from, so the sign-in screen asks first.
 *
 * It asks the session endpoint, which needs no session and is under `/api`. `/health` looks like the obvious choice
 * and is the wrong one: the hosting rewrite only forwards `/api`, so `/health` is answered by this application's own
 * index page with a `200` and the probe passes while the API is dead. That is exactly what it did.
 */
@Injectable({ providedIn: 'root' })
export class ApiReadiness {
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);

  private readonly state = signal<ApiReadinessState>('checking');

  readonly readiness = this.state.asReadonly();

  /**
   * Waits for the API, bounding each attempt and pausing between them.
   *
   * A refusal counts as an answer. `403` means nobody is signed in, which is the API working; only a gateway failure,
   * a transport error or silence means it is not there yet.
   */
  probe(): void {
    this.state.set('checking');

    this.http
      .get('/api/auth/session', { responseType: 'text' })
      .pipe(
        timeout(ATTEMPT_TIMEOUT_MS),
        catchError((error: unknown) =>
          error instanceof HttpErrorResponse && error.status >= 400 && error.status < 500
            ? of('answered')
            : throwError(() => error),
        ),
        retry({ count: ATTEMPTS_AFTER_THE_FIRST, delay: () => timer(DELAY_BETWEEN_ATTEMPTS_MS) }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: () => this.state.set('ready'),
        error: () => this.state.set('unreachable'),
      });
  }
}
