import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

import { SystemSummary } from '../../contracts/dashboard-overview';
import { Icon, IconName } from '../../ui/icon';
import { Sparkline } from '../../ui/sparkline';

interface NavigationItem {
  readonly label: string;
  readonly icon: IconName;
  /** `null` marks a screen that is planned for a later slice and therefore not navigable yet. */
  readonly route: string | null;
}

/** V1 ships the Overview screen. The remaining destinations are declared but honestly disabled. */
const NAVIGATION: readonly NavigationItem[] = [
  { label: 'Overview', icon: 'home', route: '/overview' },
  { label: 'Projects', icon: 'stacks', route: null },
  { label: 'Deployments', icon: 'rocket', route: null },
  { label: 'Health', icon: 'heartPulse', route: null },
  { label: 'Logs', icon: 'fileText', route: null },
  { label: 'Environments', icon: 'server', route: null },
  { label: 'Settings', icon: 'gear', route: null },
];

@Component({
  selector: 'co-sidebar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterLinkActive, Icon, Sparkline],
  templateUrl: './sidebar.html',
  styleUrl: './sidebar.scss',
})
export class Sidebar {
  readonly summary = input<SystemSummary | null>(null);
  readonly collapsed = input(false);
  readonly collapsedChange = output<boolean>();

  protected readonly navigation = NAVIGATION;

  protected toggle(): void {
    this.collapsedChange.emit(!this.collapsed());
  }
}
