import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type ProjectMarkTone = 'navy' | 'slate' | 'amber';

/**
 * Project mark: the hexagonal glyph from the icon system, carrying the project initial.
 *
 * Presentation only. The tone identifies the project at a glance and never encodes status.
 */
@Component({
  selector: 'co-project-mark',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: inline-flex;
    }

    text {
      font-family: var(--co-font-sans);
      font-size: 10px;
      font-weight: 700;
      letter-spacing: 0.02em;
    }

    .hex[data-tone='navy'] {
      fill: #0e1c3d;
    }

    .hex[data-tone='slate'] {
      fill: #16233f;
    }

    .hex[data-tone='amber'] {
      fill: #a9761c;
    }

    .ring {
      fill: none;
      stroke: rgba(255, 255, 255, 0.35);
      stroke-width: 1;
    }
  `,
  template: `
    <svg [attr.width]="size()" [attr.height]="size()" viewBox="0 0 24 24" role="presentation">
      <path class="hex" [attr.data-tone]="tone()" d="M12 1.8 20.6 6.7v10.6L12 22.2 3.4 17.3V6.7Z" />
      <path class="ring" d="M12 4.6 18.1 8.1v7L12 18.6 5.9 15.1v-7Z" />
      <text x="12" y="15.4" text-anchor="middle" fill="#ffffff">{{ initial() }}</text>
    </svg>
  `,
})
export class ProjectMark {
  readonly name = input.required<string>();
  readonly tone = input.required<ProjectMarkTone>();
  readonly size = input(34);

  protected readonly initial = computed(() => this.name().charAt(0).toUpperCase());
}
