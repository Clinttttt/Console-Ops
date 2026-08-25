import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { SessionStore } from './session.store';

/**
 * Who Console Ops says is signed in.
 *
 * The distinction that matters is "nobody is signed in" against "the API could not be reached": one asks an operator
 * to sign in, and the other would be asking them to fix something that is not theirs to fix.
 */
describe('SessionStore', () => {
  let store: SessionStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });

    store = TestBed.inject(SessionStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('knows nothing until it has asked', () => {
    expect(store.loadState()).toBe('unknown');
    expect(store.operator()).toBeNull();
    expect(store.isSignedIn()).toBe(false);
  });

  it('reports the operator the API names', () => {
    store.read();
    http.expectOne('/api/auth/session').flush({
      login: 'Clinttttt',
      avatarUrl: 'https://avatars.test/clint.png',
      signedInAt: '2026-08-25T09:00:00.000Z',
      expiresAt: '2026-08-25T17:00:00.000Z',
    });

    expect(store.isSignedIn()).toBe(true);
    expect(store.operator()?.login).toBe('Clinttttt');
    expect(store.operator()?.avatarUrl).toBe('https://avatars.test/clint.png');
  });

  it('treats a refused session as signed out', () => {
    store.read();
    http
      .expectOne('/api/auth/session')
      .flush({ code: 'Auth.NoSession' }, { status: 403, statusText: 'Forbidden' });

    expect(store.isSignedOut()).toBe(true);
    expect(store.isSignedIn()).toBe(false);
  });

  it('tells an unreachable API apart from a signed-out operator', () => {
    store.read();
    http.expectOne('/api/auth/session').flush(null, { status: 503, statusText: 'Unavailable' });

    // Sending somebody to sign in would not help: the API is what is unavailable.
    expect(store.loadState()).toBe('unavailable');
    expect(store.isSignedOut()).toBe(false);
  });

  it('holds no operator after a failed read', () => {
    store.read();
    http
      .expectOne('/api/auth/session')
      .flush({ login: 'Clinttttt', avatarUrl: null, signedInAt: '', expiresAt: '' });
    expect(store.operator()).not.toBeNull();

    store.read();
    http
      .expectOne('/api/auth/session')
      .flush({ code: 'Auth.NotAnOperator' }, { status: 403, statusText: 'Forbidden' });

    // An operator removed from the list stops being reported, rather than lingering from the last good read.
    expect(store.operator()).toBeNull();
  });

  it('asks the server to end the session rather than only forgetting it here', () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

    store.signOut();
    // Deleting the record is what makes a copied cookie stop working.
    http
      .expectOne((request) => request.url === '/api/auth/sign-out' && request.method === 'POST')
      .flush(null);

    expect(store.isSignedIn()).toBe(false);
    expect(navigate).toHaveBeenCalledWith('/sign-in');
  });

  it('still signs out locally when the server call fails', () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

    store.signOut();
    http.expectOne('/api/auth/sign-out').flush(null, { status: 500, statusText: 'Server Error' });

    // Whatever the server said, this browser is done with the session it was holding.
    expect(store.isSignedIn()).toBe(false);
    expect(navigate).toHaveBeenCalledWith('/sign-in');
  });

  it('does not treat an HttpErrorResponse subclass check as a signed-out answer for other codes', () => {
    store.read();
    http.expectOne('/api/auth/session').error(new ProgressEvent('network'), { status: 0 });

    expect(store.loadState()).toBe('unavailable');
    expect(HttpErrorResponse).toBeDefined();
  });
});
