import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { TopBar } from '../../core/layout/top-bar/top-bar';
import { SessionStore } from '../../core/state/session.store';
import { SignInPage } from './sign-in-page';

describe('SignInPage', () => {
  let fixture: ComponentFixture<SignInPage>;
  let host: HTMLElement;

  async function render(error: string | null = null): Promise<void> {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({ imports: [SignInPage] }).compileComponents();

    fixture = TestBed.createComponent(SignInPage);
    fixture.componentRef.setInput('error', error);
    await fixture.whenStable();
    host = fixture.nativeElement as HTMLElement;
  }

  it('offers GitHub as a navigation rather than a fetch', async () => {
    await render();
    const link = host.querySelector<HTMLAnchorElement>('.continue')!;

    // The sign-in is a redirect the server owns, so the browser has to navigate to it.
    expect(link.tagName).toBe('A');
    expect(link.getAttribute('href')).toBe('/api/auth/github/start');
  });

  it('says the token never reaches the page', async () => {
    await render();

    expect(host.textContent).toContain('keeps your GitHub token on its own server');
  });

  it('explains an account that is not an operator', async () => {
    await render('Auth.NotAnOperator');

    // Retrying is pointless with the same account, so the wording says what would actually change the outcome.
    expect(host.textContent).toContain('not an operator of this Console Ops');
    expect(host.querySelector('[role="alert"]')).not.toBeNull();
  });

  it('separates a provider outage from anything the operator did', async () => {
    await render('Auth.ProviderUnavailable');

    expect(host.textContent).toContain('Nothing is wrong with your account');
  });

  it('separates a fault in Console Ops itself from a refusal by GitHub', async () => {
    await render('unavailable');

    // The callback redirects here rather than answering a problem document, so this wording is what an
    // operator sees when the server failed mid sign-in.
    expect(host.textContent).toContain('Console Ops could not complete the sign-in');
    expect(host.textContent).toContain('recorded on the server');
  });

  it('shows an unrecognised reason as a refusal without repeating it', async () => {
    await render('<script>alert(1)</script>');

    expect(host.textContent).toContain('did not complete');
    // A redirect parameter is not somewhere to reflect an arbitrary string from.
    expect(host.textContent).not.toContain('script');
  });

  it('says nothing about a refusal when there was none', async () => {
    await render();

    expect(host.querySelector('[role="alert"]')).toBeNull();
  });
});

describe('TopBar identity', () => {
  let fixture: ComponentFixture<TopBar>;
  let host: HTMLElement;
  let http: HttpTestingController;

  async function render(): Promise<void> {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [TopBar],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(TopBar);
    fixture.componentRef.setInput('title', 'Workflows');
    await fixture.whenStable();
    host = fixture.nativeElement as HTMLElement;
    http = TestBed.inject(HttpTestingController);
  }

  async function signIn(avatarUrl: string | null): Promise<void> {
    TestBed.inject(SessionStore).read();
    http.expectOne('/api/auth/session').flush({
      login: 'Clinttttt',
      avatarUrl,
      signedInAt: '2026-08-25T09:00:00.000Z',
      expiresAt: '2026-08-25T17:00:00.000Z',
    });
    await fixture.whenStable();
  }

  beforeEach(async () => {
    await render();
  });

  it('names the GitHub account rather than a placeholder', async () => {
    await signIn('https://avatars.test/clint.png');

    expect(host.querySelector('.operator-name')?.textContent?.trim()).toBe('Clinttttt');
    // The old label described a person who does not exist: GitHub attributes a run to the account that signed in.
    expect(host.textContent).not.toContain('Local operator');
  });

  it('shows the avatar GitHub reported', async () => {
    await signIn('https://avatars.test/clint.png');

    const avatar = host.querySelector<HTMLImageElement>('.avatar-image')!;
    expect(avatar.getAttribute('src')).toBe('https://avatars.test/clint.png');
    expect(host.querySelector('.avatar')).toBeNull();
  });

  it('falls back to initials only when there is no avatar', async () => {
    await signIn(null);

    expect(host.querySelector('.avatar-image')).toBeNull();
    expect(host.querySelector('.avatar')?.textContent?.trim()).toBe('CL');
  });

  it('says nobody is signed in rather than inventing an operator', () => {
    expect(host.querySelector('.operator-name')?.textContent?.trim()).toBe('Not signed in');
  });

  it('offers sign-in when there is no session, and sign-out when there is', async () => {
    host.querySelector<HTMLButtonElement>('.operator')!.click();
    await fixture.whenStable();
    // Nothing to sign out of: the honest action is the one that gets them in.
    expect(host.querySelector('.menu')?.textContent).toContain('Sign in with GitHub');

    await signIn(null);
    expect(host.querySelector('.menu')?.textContent).toContain('Sign out');
    expect(host.querySelector('.menu')?.textContent).toContain('Clinttttt');
  });
});
