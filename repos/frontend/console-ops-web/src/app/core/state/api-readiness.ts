import { HttpClient } from '@angular/common/http';
import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { retry, timeout, timer } from 'rxjs';

export type ApiReadinessState = 'checking' | 'ready' | 'unreachable';

/** Each attempt is bounded: the proxy holds a request open while a container starts rather than refusing it. */
const ATTEMPT_TIMEOUT_MS = 8000;

/** Roughly forty seconds of patience in total, against a cold start measured at twenty-five. */
const ATTEMPTS_AFTER_THE_FIRST = 4;
const DELAY_BETWEEN_ATTEMPTS_MS = 2000;

/**
 * Whether the API is answering yet.
 *
 * The deployment scales to zero, so the first request after an idle period waits for a container to start and the
 * proxy in front of it gives up first - a `502` that means "not awake", not "broken". Sending an operator to GitHub
 * before then produces exactly that, on a page they cannot get back from.
 *
 * So the sign-in screen asks first, on the one endpoint that needs no session, and waits rather than guessing.
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
   * Giving up says so instead of leaving the screen waiting, which is the failure this whole class exists to remove.
   */
  probe(): void {
    this.state.set('checking');

    this.http
      .get('/health', { responseType: 'text' })
      .pipe(
        timeout(ATTEMPT_TIMEOUT_MS),
        retry({ count: ATTEMPTS_AFTER_THE_FIRST, delay: () => timer(DELAY_BETWEEN_ATTEMPTS_MS) }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: () => this.state.set('ready'),
        error: () => this.state.set('unreachable'),
      });
  }
}
