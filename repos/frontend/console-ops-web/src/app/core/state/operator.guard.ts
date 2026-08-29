import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { catchError, map, of, timeout } from 'rxjs';

import { OperatorSession } from '../contracts/session';
import { SessionStore } from '../state/session.store';

/** Long enough for a slow answer, short enough that nobody watches an empty page waiting for one. */
const SESSION_READ_TIMEOUT_MS = 6000;

/**
 * Keeps a screen from loading before Console Ops knows who is asking.
 *
 * The check is the API's answer, not a value held in the browser: a session lives in an `HttpOnly` cookie the app
 * cannot read, which is what stops script from getting at it. So the guard asks - once. An operator already known to
 * be signed in is admitted without a round trip, because asking again on every navigation put a request between the
 * operator and every screen, which is what made moving around feel slow.
 *
 * That is not the security boundary. The API refuses every request without a session, so a session that ends is
 * caught by the next read a screen makes, not by this.
 *
 * It also gives up asking. A proxy in front of a container that is starting holds a request open rather than
 * refusing it, so without a bound this waited long enough to look like nothing was happening at all.
 */
export const operatorGuard: CanActivateFn = (_route, state) => {
  const http = inject(HttpClient);
  const router = inject(Router);
  const sessions = inject(SessionStore);

  if (sessions.isSignedIn()) {
    return true;
  }

  return http.get<OperatorSession>('/api/auth/session').pipe(
    timeout(SESSION_READ_TIMEOUT_MS),
    map((session) => {
      // Handed over rather than read again: the answer is already here.
      sessions.accept(session);
      return true;
    }),
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && (error.status === 403 || error.status === 401)) {
        return of(router.createUrlTree(['/sign-in']));
      }

      // An unreachable API is still not a signed-out operator, and the sign-in screen says which it was. Letting the
      // screen load instead - as this did - showed the shell around an empty page and a profile reading "Not signed
      // in", which tells an operator nothing and offers them nowhere to go. A timeout arrives here too.
      //
      // Where they were going travels with them, because this case is not a refusal: an operator whose session is
      // intact and whose API was merely asleep should end up where they asked for, without signing in again.
      return of(
        router.createUrlTree(['/sign-in'], {
          queryParams: { error: 'unreachable', returnTo: state.url },
        }),
      );
    }),
  );
};
