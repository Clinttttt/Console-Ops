import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRouteSnapshot, NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';

import { Sidebar } from './core/layout/sidebar/sidebar';
import { TopBar } from './core/layout/top-bar/top-bar';
import { DashboardOverviewStore } from './core/state/dashboard-overview.store';
import { SessionStore } from './core/state/session.store';

interface PageHeader {
  readonly title: string;
  readonly subtitle: string | null;
}

const FALLBACK_HEADER: PageHeader = { title: 'Console Ops', subtitle: null };

@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, Sidebar, TopBar],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly router = inject(Router);
  private readonly overviewStore = inject(DashboardOverviewStore);
  private readonly sessions = inject(SessionStore);

  private readonly navigated = toSignal(
    this.router.events.pipe(filter((event) => event instanceof NavigationEnd)),
    { initialValue: null },
  );

  protected readonly sidebarCollapsed = signal(false);
  protected readonly summary = this.overviewStore.summary;

  /**
   * Whether to draw the console around the page.
   *
   * Only once a screen has actually been admitted. Drawing it as soon as the URL was not the sign-in page meant a
   * cold start showed the sidebar, the top bar and a profile reading "Not signed in" around nothing at all, for as
   * long as the guard waited - a console that looks broken rather than one that is starting.
   *
   * The sign-in screen stays outside it: a sidebar of destinations nobody can reach yet is a menu of dead ends.
   */
  protected readonly showsConsole = computed(() => {
    return this.navigated() !== null && !this.router.url.startsWith('/sign-in');
  });

  /** Nothing has been admitted yet, and the guard has not finished asking who is here. */
  protected readonly isStarting = computed(() => this.navigated() === null);

  /** Page title/subtitle come from route data so every screen owns its own header text. */
  protected readonly header = computed<PageHeader>(() => {
    this.navigated();
    return readHeader(this.router.routerState.snapshot.root);
  });

  constructor() {
    // Read once at startup so the top bar names the operator rather than waiting for a screen to ask.
    this.sessions.read();
  }

  protected setSidebarCollapsed(collapsed: boolean): void {
    this.sidebarCollapsed.set(collapsed);
  }
}

function readHeader(root: ActivatedRouteSnapshot): PageHeader {
  let route: ActivatedRouteSnapshot | null = root;
  let header = FALLBACK_HEADER;

  while (route !== null) {
    const title = route.data['title'];
    if (typeof title === 'string') {
      const subtitle = route.data['subtitle'];
      header = { title, subtitle: typeof subtitle === 'string' ? subtitle : null };
    }
    route = route.firstChild;
  }

  return header;
}
