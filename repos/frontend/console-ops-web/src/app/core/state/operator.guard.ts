import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { catchError, map, of } from 'rxjs';

import { OperatorSession } from '../contracts/session';
import { SessionStore } from '../state/session.store';

/**
 * Keeps a screen from loading before Console Ops knows who is asking.
 *
 * The check is the API's answer, not a value held in the browser: a session lives in an `HttpOnly` cookie the app
 * cannot read, which is what stops script from getting at it. So the guard asks.
 *
 * A Console Ops with no sign-in configured answers the session read with 404 rather than 403, and is allowed
 * through - that is local development, where there is nobody to be.
 */
export const operatorGuard: CanActivateFn = () => {
  const http = inject(HttpClient);
  const router = inject(Router);
  const sessions = inject(SessionStore);

  return http.get<OperatorSession>('/api/auth/session').pipe(
    map(() => {
      sessions.read();
      return true;
    }),
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && (error.status === 403 || error.status === 401)) {
        return of(router.createUrlTree(['/sign-in']));
      }

      // An unreachable API is not a signed-out operator. Letting the screen load means it can say the API is
      // unavailable in its own words, which is more use than a sign-in page that will not help.
      return of(true);
    }),
  );
};
