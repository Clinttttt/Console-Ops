import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

import { StatusLevel } from '../../../core/contracts/dashboard-overview';
import {
  EndpointVerification,
  VerifiedHealthState,
} from '../../../core/contracts/endpoint-verification';
import { Icon } from '../../../core/ui/icon';

/**
 * What Console Ops will observe, and what it observed when asked.
 *
 * Monitoring is a consequence of configuration: an endpoint exists, so the observation runs. Nothing
 * here is a switch. Until a check has run, this shows the configured paths and no result, because no
 * observation exists yet.
 */
@Component({
  selector: 'co-endpoint-monitoring',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, Icon],
  templateUrl: './endpoint-monitoring.html',
  styleUrl: './endpoint-monitoring.scss',
})
export class EndpointMonitoring {
  readonly healthEndpoint = input('');
  readonly versionEndpoint = input('');
  readonly verification = input<EndpointVerification | null>(null);
  readonly verifying = input(false);
  readonly error = input<string | null>(null);
  readonly canVerify = input(false);

  readonly verify = output<void>();

  /** Wording and colour for an observed health state; both come from the same fact. */
  protected label(state: VerifiedHealthState): string {
    switch (state) {
      case 'healthy':
        return 'Healthy';
      case 'degraded':
        return 'Degraded';
      case 'unhealthy':
        return 'Unhealthy';
      case 'unreachable':
        return 'Unreachable';
      case 'notConfigured':
        return 'Not configured';
      default:
        return 'Unknown';
    }
  }

  protected level(state: VerifiedHealthState): StatusLevel {
    switch (state) {
      case 'healthy':
        return 'healthy';
      case 'degraded':
        return 'degraded';
      case 'unhealthy':
      case 'unreachable':
        return 'down';
      case 'notConfigured':
        return 'notApplicable';
      default:
        return 'unknown';
    }
  }
}
