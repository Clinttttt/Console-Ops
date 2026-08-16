import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  output,
  signal,
} from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';

import { AzureLogSource } from '../../../core/contracts/azure-discovery';
import { AzureDiscoveryDataSource } from '../../../core/data/azure-discovery.data-source';
import { Icon } from '../../../core/ui/icon';

/** Why the container app list could not be read. */
type DiscoveryFailure = 'credential' | 'rateLimited' | 'apiUnavailable' | 'unknown';

/**
 * Log source picker.
 *
 * Lists the container apps the configured Azure identity can see, with the workspace each one's
 * environment logs to, so the operator picks a source instead of typing a GUID. One choice fills both
 * fields, because both come from the same Azure resource.
 *
 * Discovery may prefill but never decide: the fields stay editable, an app whose environment has no log
 * configuration is shown as unavailable rather than offered, and a failure says which part failed instead
 * of leaving an empty list that looks like an empty tenant.
 */
@Component({
  selector: 'co-azure-log-source-picker',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon],
  templateUrl: './azure-log-source-picker.html',
  styleUrl: './azure-log-source-picker.scss',
})
export class AzureLogSourcePicker {
  private readonly discovery = inject(AzureDiscoveryDataSource);

  readonly choose = output<AzureLogSource>();

  /** Self-contained: the picker owns its trigger, so no screen has to restate the control. */
  protected readonly open = signal(false);
  protected readonly query = signal('');

  private readonly sources = rxResource({
    // Asked for deliberately: closed means Azure is never called.
    params: () => (this.open() ? this.query().trim() : undefined),
    stream: ({ params }) => this.discovery.listLogSources(params),
  });

  protected readonly isLoading = computed(() => this.sources.isLoading());
  protected readonly failed = computed(() => this.sources.error() !== undefined);
  protected readonly page = computed(() => this.sources.value() ?? null);

  /**
   * Why discovery failed, in the operator's terms. The API reports a stable code, so a missing Azure
   * sign-in is not blamed on the network.
   */
  protected readonly failureReason = computed<DiscoveryFailure>(() => {
    const error = this.sources.error();
    if (!(error instanceof HttpErrorResponse)) {
      return 'unknown';
    }

    const code: unknown = error.error?.code;
    if (code === 'Azure.Unauthorized') {
      return 'credential';
    }
    if (code === 'Azure.RateLimited') {
      return 'rateLimited';
    }
    return error.status === 0 ? 'apiUnavailable' : 'unknown';
  });

  protected toggle(): void {
    this.open.update((open) => !open);
  }

  protected select(source: AzureLogSource): void {
    this.choose.emit(source);
    this.open.set(false);
  }

  protected setQuery(query: string): void {
    this.query.set(query);
  }

  protected retry(): void {
    this.sources.reload();
  }
}
