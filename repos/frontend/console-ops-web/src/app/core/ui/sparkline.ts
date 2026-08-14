import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * Minimal inline sparkline. Renders nothing but an explicit dash when no samples exist, so the UI
 * never implies history that has not been recorded.
 */
@Component({
  selector: 'co-sparkline',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
    }

    polyline {
      fill: none;
      stroke: var(--co-status-running);
      stroke-width: 1.25;
      stroke-linejoin: round;
      stroke-linecap: round;
      opacity: 0.75;
    }
  `,
  template: `
    @if (points() === null) {
      <span class="co-unavailable">&mdash;</span>
    } @else {
      <svg
        [attr.width]="width()"
        [attr.height]="height()"
        [attr.viewBox]="'0 0 ' + width() + ' ' + height()"
        role="img"
        [attr.aria-label]="label()"
      >
        <polyline [attr.points]="points()" />
      </svg>
    }
  `,
})
export class Sparkline {
  readonly samples = input.required<readonly number[]>();
  readonly width = input(44);
  readonly height = input(16);
  readonly label = input('Recent trend');

  protected readonly points = computed(() => {
    const samples = this.samples();
    if (samples.length < 2) {
      return null;
    }

    const min = Math.min(...samples);
    const max = Math.max(...samples);
    const span = max - min || 1;
    const stepX = this.width() / (samples.length - 1);
    const usableHeight = this.height() - 2;

    return samples
      .map((sample, index) => {
        const x = index * stepX;
        const y = 1 + usableHeight - ((sample - min) / span) * usableHeight;
        return `${x.toFixed(1)},${y.toFixed(1)}`;
      })
      .join(' ');
  });
}
