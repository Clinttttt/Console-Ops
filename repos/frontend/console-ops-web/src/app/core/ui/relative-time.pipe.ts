import { Pipe, PipeTransform } from '@angular/core';

const MINUTE = 60_000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

/**
 * Formats an instant relative to an explicit reference instant, normally the payload's `observedAt`.
 *
 * Measuring against the observation rather than the browser clock keeps the wording truthful: "18 min
 * ago" means 18 minutes before Console Ops observed the fact, which is what the operator needs to
 * judge freshness. It also keeps the output deterministic and testable.
 *
 * Returns `null` when either instant is missing so templates can render an explicit unavailable state.
 */
@Pipe({ name: 'coRelativeTime' })
export class RelativeTimePipe implements PipeTransform {
  transform(
    instant: string | null | undefined,
    reference: string | null | undefined,
  ): string | null {
    if (
      instant === null ||
      instant === undefined ||
      reference === null ||
      reference === undefined
    ) {
      return null;
    }

    const elapsed = Date.parse(reference) - Date.parse(instant);
    if (Number.isNaN(elapsed)) {
      return null;
    }

    if (elapsed < 0) {
      return 'just now';
    }

    if (elapsed < MINUTE) {
      return `${Math.floor(elapsed / 1000)} sec ago`;
    }

    if (elapsed < HOUR) {
      return `${Math.floor(elapsed / MINUTE)} min ago`;
    }

    if (elapsed < DAY) {
      const hours = Math.floor(elapsed / HOUR);
      return `${hours} hr ago`;
    }

    const days = Math.floor(elapsed / DAY);
    return days === 1 ? '1 day ago' : `${days} days ago`;
  }
}
