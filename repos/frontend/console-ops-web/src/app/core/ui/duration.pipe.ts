import { Pipe, PipeTransform } from '@angular/core';

/**
 * Formats a duration in seconds as `2m 18s`, `48s`, or `1h 04m`.
 *
 * Returns `null` when the duration is unknown so templates can render an explicit unavailable state
 * instead of a misleading zero.
 */
@Pipe({ name: 'coDuration' })
export class DurationPipe implements PipeTransform {
  transform(seconds: number | null | undefined): string | null {
    if (seconds === null || seconds === undefined || !Number.isFinite(seconds) || seconds < 0) {
      return null;
    }

    const whole = Math.round(seconds);
    if (whole < 60) {
      return `${whole}s`;
    }

    const minutes = Math.floor(whole / 60);
    const remainder = whole % 60;

    if (minutes < 60) {
      return `${minutes}m ${String(remainder).padStart(2, '0')}s`;
    }

    const hours = Math.floor(minutes / 60);
    return `${hours}h ${String(minutes % 60).padStart(2, '0')}m`;
  }
}
