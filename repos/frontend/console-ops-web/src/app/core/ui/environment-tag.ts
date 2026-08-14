import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { EnvironmentKind } from '../contracts/dashboard-overview';
import { Icon } from './icon';

/**
 * Environment tag. A production target must never be mistaken for a local one, so the kind drives
 * both the glyph and the emphasis.
 */
@Component({
  selector: 'co-environment-tag',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon],
  styles: `
    .tag {
      display: inline-flex;
      align-items: center;
      gap: var(--co-space-2);
      padding: 4px 9px;
      border: 1px solid var(--co-line-strong);
      border-radius: var(--co-radius-md);
      font-size: 11.5px;
      color: var(--co-ink-muted);
      background: var(--co-surface);
      white-space: nowrap;
    }

    .tag[data-kind='production'] {
      border-color: #cfdff7;
      background: var(--co-blue-wash);
      color: var(--co-blue);
      font-weight: 600;
    }

    .tag[data-kind='staging'],
    .tag[data-kind='development'] {
      color: var(--co-ink);
    }
  `,
  template: `
    <span class="tag" [attr.data-kind]="kind()">
      <co-icon [name]="glyph()" [size]="13" />
      {{ name() }}
    </span>
  `,
})
export class EnvironmentTag {
  readonly name = input.required<string>();
  readonly kind = input.required<EnvironmentKind>();

  protected readonly glyph = computed<'monitor' | 'cloud'>(() =>
    this.kind() === 'local' ? 'monitor' : 'cloud',
  );
}
