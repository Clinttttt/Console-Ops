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

import { GitHubRepository } from '../../../core/contracts/github-discovery';
import { GitHubDiscoveryDataSource } from '../../../core/data/github-discovery.data-source';
import { Icon } from '../../../core/ui/icon';
import { RelativeTimePipe } from '../../../core/ui/relative-time.pipe';

/** Why the repository list could not be read. */
type DiscoveryFailure =
  'credential' | 'rateLimited' | 'notImplemented' | 'apiUnavailable' | 'unknown';

/**
 * Repository picker for the import path.
 *
 * Lists what the configured GitHub credential can see so the operator selects rather than types. When
 * the discovery endpoint is unavailable - it is not implemented yet - the picker says so plainly and the
 * caller keeps the manual repository field, instead of the screen inventing repositories.
 */
@Component({
  selector: 'co-github-repository-picker',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon, RelativeTimePipe],
  templateUrl: './github-repository-picker.html',
  styleUrl: './github-repository-picker.scss',
})
export class GitHubRepositoryPicker {
  private readonly discovery = inject(GitHubDiscoveryDataSource);

  readonly choose = output<GitHubRepository>();
  readonly dismiss = output<void>();

  protected readonly query = signal('');

  private readonly repositories = rxResource({
    params: () => this.query().trim(),
    stream: ({ params }) => this.discovery.listRepositories(params),
  });

  protected readonly isLoading = computed(() => this.repositories.isLoading());
  protected readonly failed = computed(() => this.repositories.error() !== undefined);
  protected readonly page = computed(() => this.repositories.value() ?? null);

  /**
   * Why discovery failed, in the operator's terms.
   *
   * The API reports a stable code in its problem details, so the picker can distinguish a missing
   * GitHub credential from an endpoint that does not exist yet, instead of blaming the wrong thing.
   */
  protected readonly failureReason = computed<DiscoveryFailure>(() => {
    const error = this.repositories.error();
    if (!(error instanceof HttpErrorResponse)) {
      return 'unknown';
    }

    const code: unknown = error.error?.code;
    if (code === 'GitHub.Unauthorized') {
      return 'credential';
    }
    if (code === 'GitHub.RateLimited') {
      return 'rateLimited';
    }
    if (error.status === 404) {
      return 'notImplemented';
    }
    return error.status === 0 ? 'apiUnavailable' : 'unknown';
  });

  /** Reference instant for "updated" times: the browser clock is the only clock available here. */
  protected readonly now = new Date().toISOString();

  protected setQuery(query: string): void {
    this.query.set(query);
  }

  protected retry(): void {
    this.repositories.reload();
  }
}
