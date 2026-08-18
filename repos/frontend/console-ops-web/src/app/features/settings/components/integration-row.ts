import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

import { Integration } from '../../../core/contracts/settings';
import { Icon, IconName } from '../../../core/ui/icon';

/**
 * One provider Console Ops depends on.
 *
 * Configuration and verification are shown as two separate facts on purpose. "A key is present" and "the
 * provider answered" are different claims, and only the second is evidence that anything works. Until a probe
 * runs, verification reads as not yet checked rather than as either good or bad.
 */
@Component({
  selector: 'co-integration-row',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, Icon],
  templateUrl: './integration-row.html',
  styleUrl: './integration-row.scss',
})
export class IntegrationRow {
  readonly integration = input.required<Integration>();
  readonly isTesting = input(false);

  readonly test = output<void>();

  protected readonly icon = computed<IconName>(() =>
    this.integration().id === 'github' ? 'github' : 'azure',
  );

  /** The headline state: what the operator reads first. Verified outranks configured, because it is proof. */
  protected readonly status = computed(() => {
    const integration = this.integration();
    if (integration.configuration === 'notConfigured') {
      return { label: 'Not configured', level: 'unknown' as const };
    }

    if (integration.verification === 'failed') {
      return { label: 'Connection failed', level: 'down' as const };
    }

    if (integration.verification === 'verified') {
      return { label: 'Verified', level: 'healthy' as const };
    }

    return integration.configuration === 'partial'
      ? { label: 'Partly configured', level: 'warning' as const }
      : { label: 'Configured', level: 'unknown' as const };
  });
}
