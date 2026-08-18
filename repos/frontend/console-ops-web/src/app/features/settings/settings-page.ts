import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { Integration } from '../../core/contracts/settings';
import { SettingsStore } from '../../core/state/settings.store';
import { Icon } from '../../core/ui/icon';
import { IntegrationRow } from './components/integration-row';

/** One line in the rail: a capability and how it currently stands. */
interface RailEntry {
  readonly label: string;
  readonly icon: 'github' | 'azure' | 'refresh' | 'database';
  readonly state: string;
  readonly level: 'healthy' | 'warning' | 'down' | 'unknown';
}

/**
 * Settings.
 *
 * Describes Console Ops itself: how it reaches its providers and how often it observes. Project, environment,
 * deployment and health facts are deliberately absent - they have their own screens, and repeating them here
 * would create a second place to look for one fact.
 *
 * Nothing on this screen is editable yet, because nothing here can be persisted at runtime: the interval comes
 * from application settings. A control that looks editable and silently forgets is worse than a stated value.
 */
@Component({
  selector: 'co-settings-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, Icon, IntegrationRow, RouterLink],
  templateUrl: './settings-page.html',
  styleUrl: './settings-page.scss',
})
export class SettingsPage {
  private readonly store = inject(SettingsStore);

  protected readonly loadState = this.store.loadState;
  protected readonly integrations = this.store.integrations;
  protected readonly collection = this.store.collection;
  protected readonly about = this.store.about;
  protected readonly isSampleData = this.store.isSampleData;
  protected readonly failure = this.store.failure;
  protected readonly runningAction = this.store.runningAction;

  constructor() {
    this.store.read();
  }

  /** The interval as an operator would say it, not as seconds always. */
  protected readonly interval = computed(() => {
    const seconds = this.collection()?.intervalSeconds ?? null;
    if (seconds === null) {
      return null;
    }

    if (seconds < 60) {
      return `${seconds} seconds`;
    }

    const minutes = Math.round(seconds / 60);
    return minutes === 1 ? '1 minute' : `${minutes} minutes`;
  });

  protected readonly sweepDuration = computed(() => {
    const milliseconds = this.collection()?.lastSweepMilliseconds ?? null;
    if (milliseconds === null) {
      return null;
    }

    return milliseconds >= 1000
      ? `${(milliseconds / 1000).toFixed(1)} s`
      : `${Math.round(milliseconds)} ms`;
  });

  /**
   * The rail restates the verdicts, not the detail. Green means verified or working; a configured integration
   * that has not answered yet stays grey, because presence is not proof.
   */
  protected readonly rail = computed<readonly RailEntry[]>(() => {
    const entries: RailEntry[] = this.integrations().map((integration) => ({
      label: integration.name,
      icon: integration.id,
      ...this.railState(integration),
    }));

    const collection = this.collection();
    if (collection !== null) {
      entries.push({
        label: 'Collection',
        icon: 'refresh',
        state: collection.isEnabled ? 'Running' : 'Off',
        level: collection.isEnabled ? 'healthy' : 'unknown',
      });
    }

    const about = this.about();
    if (about !== null) {
      entries.push({
        label: 'Database',
        icon: 'database',
        state: about.databaseSchema === 'upToDate' ? 'Up to date' : 'Migrations pending',
        level: about.databaseSchema === 'upToDate' ? 'healthy' : 'warning',
      });
    }

    return entries;
  });

  protected test(): void {
    this.store.probe();
  }

  protected collectNow(): void {
    this.store.collectNow();
  }

  private railState(integration: Integration): Pick<RailEntry, 'state' | 'level'> {
    if (integration.configuration === 'notConfigured') {
      return { state: 'Not configured', level: 'unknown' };
    }

    if (integration.verification === 'failed') {
      return { state: 'Failed', level: 'down' };
    }

    return integration.verification === 'verified'
      ? { state: 'Verified', level: 'healthy' }
      : { state: 'Configured', level: 'unknown' };
  }
}
