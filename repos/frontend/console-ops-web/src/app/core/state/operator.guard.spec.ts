import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, UrlTree, provideRouter } from '@angular/router';
import { Observable, isObservable, lastValueFrom, of } from 'rxjs';

import { operatorGuard } from './operator.guard';

/**
 * Where an operator without a usable session ends up.
 *
 * Both refusals lead to the sign-in screen, which is the only page that can help. What differs is what it is told to
 * say: being signed out and the API not answering are not the same thing, and only one of them is about the operator.
 */
describe('operatorGuard', () => {
  let http: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });

    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => http.verify());

  async function activate(): Promise<boolean | UrlTree> {
    const result = TestBed.runInInjectionContext(() =>
      operatorGuard({} as never, {} as never),
    ) as Observable<boolean | UrlTree>;

    const decision = await lastValueFrom(isObservable(result) ? result : of(result));

    return decision as boolean | UrlTree;
  }

  it('lets a signed-in operator through', async () => {
    const decision = activate();
    const session = {
      login: 'Clinttttt',
      avatarUrl: null,
      signedInAt: '2026-08-27T09:00:00.000Z',
      expiresAt: '2026-08-27T17:00:00.000Z',
    };

    // Twice, because the guard asks and then has the store ask again so the top bar has an operator to show. Worth
    // knowing rather than hiding: it is two identical reads per guarded navigation.
    http.match('/api/auth/session').forEach((request) => request.flush(session));

    expect(await decision).toBe(true);
    http.match('/api/auth/session').forEach((request) => request.flush(session));
  });

  it('sends a signed-out operator to sign in, with nothing to explain', async () => {
    const decision = activate();
    http
      .expectOne('/api/auth/session')
      .flush({ code: 'Auth.NoSession' }, { status: 403, statusText: 'Forbidden' });

    expect(router.serializeUrl((await decision) as UrlTree)).toBe('/sign-in');
  });

  /**
   * This used to load the screen instead, which showed the shell around an empty page and a profile reading "Not
   * signed in" - no explanation, and nowhere to go. A deployment that scales to zero makes it the common case.
   */
  it('sends an operator to sign in when the API did not answer, and says so', async () => {
    const decision = activate();
    http.expectOne('/api/auth/session').flush('', { status: 502, statusText: 'Bad Gateway' });

    expect(router.serializeUrl((await decision) as UrlTree)).toBe('/sign-in?error=unreachable');
  });
});
