import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

import { LogStreamNoise } from '../../../core/contracts/log-stream';

/**
 * What the stream says when it has nothing to show.
 *
 * Three situations that look identical and mean different things: the operator's filters are too narrow, the
 * service was quiet, or everything the service said was framework logging. One generic "no events" message
 * answers none of them, and the last case is the one that matters most - an operator has to be able to tell
 * an idle service from a broken log source without turning anything off.
 *
 * Extracted from the stream so the explanation has room to be a sentence rather than a label, and so the
 * stream's own styles stay inside their budget.
 */
@Component({
  selector: 'co-log-empty',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './log-empty.html',
  styleUrl: './log-empty.scss',
})
export class LogStreamEmpty {
  readonly filtersActive = input(false);
  readonly noise = input<LogStreamNoise | null>(null);

  readonly clearFilters = output<void>();
  readonly showNoise = output<void>();

  /**
   * Which of the three answers applies. Filters come first: a narrow filter is the operator's own doing, and
   * explaining anything else would blame the service for it.
   */
  protected readonly reason = computed<'filtered' | 'noiseOnly' | 'quiet'>(() => {
    if (this.filtersActive()) {
      return 'filtered';
    }

    const hidden = this.noise();
    return hidden !== null && hidden.excluded && hidden.hiddenCount > 0 ? 'noiseOnly' : 'quiet';
  });
}
