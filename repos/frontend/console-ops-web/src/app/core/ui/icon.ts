import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * Console Ops icon set.
 *
 * Follows the project icon system: two-tone line work on a 24px grid, 1.7 stroke, with brand marks
 * drawn as filled paths. Names are semantic so a screen asks for `rocket` or `database` rather than
 * a file path, and the set stays a closed union so templates remain type-checked.
 *
 * Only icons that are actually rendered live here. Add a new entry when a screen needs it.
 */
export type IconName =
  // Navigation
  | 'home'
  | 'stacks'
  | 'rocket'
  | 'heartPulse'
  | 'fileText'
  | 'server'
  | 'gear'
  // Pipeline and providers
  | 'github'
  | 'ciCd'
  | 'cube'
  // Components
  | 'api'
  | 'database'
  | 'redis'
  | 'refresh'
  | 'codeWindow'
  | 'pulse'
  // Environment
  | 'cloud'
  | 'monitor'
  // Controls
  | 'search'
  | 'bell'
  | 'chevronDown'
  | 'chevronLeft'
  | 'arrowRight'
  | 'check'
  | 'checkCircle'
  | 'copy'
  | 'externalLink';

interface IconDefinition {
  readonly paths: readonly string[];
  /** Brand marks are filled rather than stroked. */
  readonly filled?: true;
}

const CIRCLE = (cx: number, cy: number, r: number): string =>
  `M${cx - r} ${cy}a${r} ${r} 0 1 0 ${r * 2} 0a${r} ${r} 0 1 0 ${-r * 2} 0`;

const ICONS: Readonly<Record<IconName, IconDefinition>> = {
  home: {
    paths: ['M3.6 10.4 12 3.5l8.4 6.9V20a.8.8 0 0 1-.8.8h-4.4v-6h-6.4v6H4.4a.8.8 0 0 1-.8-.8Z'],
  },
  stacks: {
    paths: ['M12 3.2 21 7.6 12 12 3 7.6Z', 'M3 12.1 12 16.5 21 12.1', 'M3 16.4 12 20.8 21 16.4'],
  },
  rocket: {
    paths: [
      'M13.6 4.2c3.6-1.2 6.4 1.6 5.2 5.2-.9 2.7-4.1 6-7.6 8.1L6.5 12.8c2.1-3.5 5.4-6.7 7.1-8.6Z',
      CIRCLE(14.7, 9.3, 1.5),
      'M7.4 16.6 4.6 19.4M9.6 18.4 8 20M5.6 14.4 4 16',
    ],
  },
  heartPulse: {
    paths: [
      'M20.2 8.9c0 4.3-5.5 8.2-8.2 10.1-2.7-1.9-8.2-5.8-8.2-10.1a4.2 4.2 0 0 1 8.2-2.3 4.2 4.2 0 0 1 8.2 2.3Z',
      'M6.8 11.6h2.6l1.3-2.4 1.7 4.3 1.3-1.9h2.5',
    ],
  },
  fileText: {
    paths: [
      'M6 3.6h7l5 4.9V20a.8.8 0 0 1-.8.8H6.8A.8.8 0 0 1 6 20Z',
      'M13 3.6v5h5',
      'M9 13h6M9 16.6h4',
    ],
  },
  server: {
    paths: [
      'M4.4 4.6h15.2v5.2H4.4Z',
      'M4.4 14.2h15.2v5.2H4.4Z',
      'M7.4 7.2h.02M7.4 16.8h.02M10.4 7.2h3M10.4 16.8h3',
    ],
  },
  gear: {
    paths: [
      CIRCLE(12, 12, 6.6),
      CIRCLE(12, 12, 3),
      // Eight radial teeth at 45-degree steps.
      'M18.6 12h2.2M5.4 12H3.2M12 18.6v2.2M12 5.4V3.2',
      'M16.67 16.67 18.22 18.22M7.33 7.33 5.78 5.78M7.33 16.67 5.78 18.22M16.67 7.33 18.22 5.78',
    ],
  },
  github: {
    filled: true,
    paths: [
      'M12 2.2A9.8 9.8 0 0 0 8.9 21.3c.5.1.7-.2.7-.5v-1.9c-2.7.6-3.3-1.2-3.3-1.2-.4-1.1-1.1-1.4-1.1-1.4-.9-.6.1-.6.1-.6 1 .1 1.5 1 1.5 1 .9 1.5 2.3 1.1 2.9.8.1-.6.4-1.1.6-1.3-2.2-.3-4.5-1.1-4.5-4.9 0-1.1.4-2 1-2.6-.1-.3-.4-1.3.1-2.6 0 0 .8-.3 2.7 1a9.3 9.3 0 0 1 5 0c1.9-1.3 2.7-1 2.7-1 .5 1.3.2 2.3.1 2.6.6.6 1 1.5 1 2.6 0 3.8-2.3 4.6-4.5 4.9.4.3.7.9.7 1.9v2.8c0 .3.2.6.7.5A9.8 9.8 0 0 0 12 2.2Z',
    ],
  },
  ciCd: {
    paths: [
      CIRCLE(7, 6.6, 2.4),
      CIRCLE(17, 12, 2.4),
      CIRCLE(7, 17.4, 2.4),
      'M9.2 7.7l5.6 3.1M9.2 16.3l5.6-3.1',
    ],
  },
  cube: {
    paths: ['M12 3.4 20 7.8v8.4L12 20.6 4 16.2V7.8Z', 'M4 7.8 12 12.2l8-4.4M12 12.2v8.4'],
  },
  api: {
    paths: [
      'M4.4 5.4h15.2v13.2H4.4Z',
      'M9.4 10.4 7.4 12l2 1.6M14.6 10.4 16.6 12l-2 1.6',
      'M12.6 9.6l-1.2 4.8',
    ],
  },
  database: {
    paths: [
      'M5 6.6c0-1.5 3.1-2.7 7-2.7s7 1.2 7 2.7-3.1 2.7-7 2.7-7-1.2-7-2.7Z',
      'M5 6.6v10.8c0 1.5 3.1 2.7 7 2.7s7-1.2 7-2.7V6.6',
      'M5 12c0 1.5 3.1 2.7 7 2.7s7-1.2 7-2.7',
    ],
  },
  redis: {
    paths: ['M12 3.6 20 7l-8 3.4L4 7Z', 'M4 11.2 12 14.6 20 11.2', 'M4 15.4 12 18.8 20 15.4'],
  },
  refresh: {
    paths: ['M19.6 12a7.6 7.6 0 1 1-2.3-5.4', 'M19.8 4.6v4.2h-4.2'],
  },
  codeWindow: {
    paths: [
      'M4.4 5.4h15.2v13.2H4.4Z',
      'M4.4 8.8h15.2',
      'M9.6 12.2 8 13.8l1.6 1.6M14.4 12.2 16 13.8l-1.6 1.6',
    ],
  },
  pulse: {
    paths: ['M3.4 12.4h3.2l2-5.4 3.2 9.6 2.2-4.2h6.6'],
  },
  cloud: {
    paths: ['M7.6 18.4h9a3.9 3.9 0 0 0 .4-7.8 5.4 5.4 0 0 0-10.2-1.4 4 4 0 0 0 .8 9.2Z'],
  },
  monitor: {
    paths: ['M3.6 5.2h16.8v10.2H3.6Z', 'M9 19.2h6M12 15.4v3.8'],
  },
  search: {
    paths: [CIRCLE(11, 11, 6.4), 'M15.8 15.8 20 20'],
  },
  bell: {
    paths: ['M18.2 15.6V10a6.2 6.2 0 1 0-12.4 0v5.6L4.4 18h15.2Z', 'M10.2 21h3.6'],
  },
  chevronDown: { paths: ['M6.8 9.6 12 14.8l5.2-5.2'] },
  chevronLeft: { paths: ['M14.4 6.8 9.2 12l5.2 5.2'] },
  arrowRight: { paths: ['M4.6 12h14.8M14 6.6l5.4 5.4-5.4 5.4'] },
  check: { paths: ['M5.6 12.6 10 17l8.4-9.4'] },
  checkCircle: { paths: [CIRCLE(12, 12, 8.4), 'M8.4 12.2 11 14.8l4.6-5'] },
  copy: {
    paths: [
      'M9.4 9.4h9.2v9.2H9.4Z',
      'M15 9.4V6.2a.8.8 0 0 0-.8-.8H6.2a.8.8 0 0 0-.8.8v8a.8.8 0 0 0 .8.8h3.2',
    ],
  },
  externalLink: {
    paths: [
      'M14 5h5v5',
      'M19 5l-7.4 7.4',
      'M17 14.4V18a1.4 1.4 0 0 1-1.4 1.4H6.4A1.4 1.4 0 0 1 5 18V8.8a1.4 1.4 0 0 1 1.4-1.4H10',
    ],
  },
};

/** Presentational icon. Decorative by default; pass a `label` to expose it to assistive tech. */
@Component({
  selector: 'co-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'co-icon' },
  styles: `
    .co-icon {
      display: inline-flex;
    }
  `,
  template: `
    <svg
      [attr.width]="size()"
      [attr.height]="size()"
      viewBox="0 0 24 24"
      [attr.fill]="definition().filled ? 'currentColor' : 'none'"
      [attr.stroke]="definition().filled ? 'none' : 'currentColor'"
      [attr.stroke-width]="strokeWidth()"
      stroke-linecap="round"
      stroke-linejoin="round"
      [attr.aria-hidden]="label() === null ? 'true' : null"
      [attr.role]="label() === null ? 'presentation' : 'img'"
      [attr.aria-label]="label()"
    >
      @for (path of definition().paths; track $index) {
        <path [attr.d]="path" />
      }
    </svg>
  `,
})
export class Icon {
  readonly name = input.required<IconName>();
  readonly size = input(16);
  readonly strokeWidth = input(1.7);
  readonly label = input<string | null>(null);

  protected readonly definition = computed(() => ICONS[this.name()]);
}
