import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { StatusCell } from '../contracts/dashboard-overview';

/**
 * Renders one operational fact: an optional status dot, its label, and optional supporting detail.
 * A `null` cell means the platform has nothing to report and is rendered as an explicit dash.
 */
@Component({
  selector: 'co-status',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
    }

    .line {
      display: flex;
      align-items: center;
      gap: var(--co-space-2);
      font-size: 12.5px;
      color: var(--co-ink);
    }

    .detail {
      margin-top: 2px;
      font-size: 11.5px;
      color: var(--co-ink-muted);
    }

    .indent {
      padding-left: 15px;
    }
  `,
  template: `
    @let value = cell();
    @if (value === null) {
      <span class="line co-unavailable" [class.indent]="reserveIndicatorSpace()">&mdash;</span>
    } @else {
      <span class="line">
        @if (value.level !== null) {
          <span class="co-dot" [attr.data-level]="value.level"></span>
        }
        <span [class.indent]="value.level === null && reserveIndicatorSpace()">{{
          value.label
        }}</span>
      </span>
      @if (value.detail !== null && showDetail()) {
        <span class="detail" [class.indent]="reserveIndicatorSpace()">{{ value.detail }}</span>
      }
    }
  `,
})
export class Status {
  readonly cell = input.required<StatusCell | null>();
  readonly showDetail = input(true);
  /** Keeps labels aligned in a column where some rows have no status dot. */
  readonly reserveIndicatorSpace = input(false);
}
