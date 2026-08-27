import { HttpClient } from '@angular/common/http';
import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { retry, timer } from 'rxjs';

export type ApiReadinessState = 'checking' | 'ready' | 'unreachable';

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
   * Waits for the API, backing off between attempts.
   *
   * Spread over roughly half a minute because a cold start has been measured at twenty-five seconds. Giving up says
   * so instead of leaving the screen waiting forever.
   */
  probe(): void {
    this.state.set('checking');

    this.http
      .get('/health', { responseType: 'text' })
      .pipe(
        retry({ count: 5, delay: (_, attempt) => timer(Math.min(2000 * attempt, 8000)) }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: () => this.state.set('ready'),
        error: () => this.state.set('unreachable'),
      });
  }
}
