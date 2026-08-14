import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRouteSnapshot, NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';

import { Sidebar } from './core/layout/sidebar/sidebar';
import { TopBar } from './core/layout/top-bar/top-bar';
import { DashboardOverviewStore } from './core/state/dashboard-overview.store';

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

  private readonly navigated = toSignal(
    this.router.events.pipe(filter((event) => event instanceof NavigationEnd)),
    { initialValue: null },
  );

  protected readonly sidebarCollapsed = signal(false);
  protected readonly summary = this.overviewStore.summary;

  /** Page title/subtitle come from route data so every screen owns its own header text. */
  protected readonly header = computed<PageHeader>(() => {
    this.navigated();
    return readHeader(this.router.routerState.snapshot.root);
  });

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
