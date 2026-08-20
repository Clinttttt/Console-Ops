import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';

import { Icon } from './icon';

/**
 * A select whose open list is Console Ops' own.
 *
 * The native control could not be styled past its closed state: the option list is drawn by the browser, and on
 * Windows it arrives as a white box with a system-blue highlight that belongs to no design here.
 *
 * Replacing it means re-earning what the native one gave for free, so this implements the combobox pattern rather
 * than a styled div: arrow keys move, Enter and Space choose, Escape closes and returns focus, Home and End jump,
 * the trigger reports expansion, and the list and its options carry the roles a screen reader needs.
 */
@Component({
  selector: 'co-select',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon],
  template: `
    <button
      #trigger
      type="button"
      class="trigger"
      role="combobox"
      [attr.aria-expanded]="open()"
      [attr.aria-controls]="listId"
      [attr.aria-label]="label()"
      [disabled]="disabled()"
      (click)="toggle()"
      (keydown)="onTriggerKey($event)"
    >
      <span class="current">{{ selectedLabel() }}</span>
      <co-icon name="chevronDown" [size]="15" />
    </button>

    @if (open()) {
      <ul class="list" role="listbox" [attr.id]="listId" [attr.aria-label]="label()">
        @for (option of options(); track option; let index = $index) {
          <li
            class="option"
            role="option"
            tabindex="-1"
            [class.is-active]="index === active()"
            [attr.aria-selected]="option === value()"
            (click)="choose(option)"
            (keydown)="onListKey($event)"
            (mouseenter)="active.set(index)"
          >
            <span class="option-label">{{ option }}</span>
            @if (option === value()) {
              <co-icon name="check" [size]="15" />
            }
          </li>
        }
      </ul>
    }
  `,
  styleUrl: './select.scss',
})
export class Select {
  readonly options = input.required<readonly string[]>();
  readonly value = input.required<string>();
  /** Names the control for a screen reader, since the visible label sits outside it. */
  readonly label = input.required<string>();
  readonly disabled = input(false);

  readonly changed = output<string>();

  private static nextId = 0;

  protected readonly open = signal(false);
  protected readonly active = signal(0);

  /** Unique so a screen reader can tie the trigger to its own list when several are on screen. */
  protected readonly listId = `co-select-${(Select.nextId += 1)}`;

  private readonly host = inject(ElementRef<HTMLElement>);

  /** Empty rather than a placeholder: the caller always passes a value, and inventing one would be a guess. */
  protected readonly selectedLabel = computed(() => this.value());

  protected toggle(): void {
    if (this.disabled()) {
      return;
    }

    const opening = !this.open();
    this.open.set(opening);

    if (opening) {
      this.active.set(Math.max(0, this.options().indexOf(this.value())));
      // Focus moves into the list so the arrow keys land there rather than scrolling the dialog behind it.
      queueMicrotask(() => this.focusActive());
    }
  }

  protected choose(option: string): void {
    this.open.set(false);
    this.focusTrigger();

    if (option !== this.value()) {
      this.changed.emit(option);
    }
  }

  protected onTriggerKey(event: KeyboardEvent): void {
    if (event.key === 'ArrowDown' || event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      if (!this.open()) {
        this.toggle();
      }
    }
  }

  protected onListKey(event: KeyboardEvent): void {
    const options = this.options();
    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        this.move(1);
        break;
      case 'ArrowUp':
        event.preventDefault();
        this.move(-1);
        break;
      case 'Home':
        event.preventDefault();
        this.active.set(0);
        this.focusActive();
        break;
      case 'End':
        event.preventDefault();
        this.active.set(options.length - 1);
        this.focusActive();
        break;
      case 'Enter':
      case ' ':
        event.preventDefault();
        this.choose(options[this.active()] ?? this.value());
        break;
      case 'Escape':
      case 'Tab':
        // Escape closes without choosing, which is the difference between browsing a list and changing a ref.
        this.open.set(false);
        this.focusTrigger();
        break;
      default:
        break;
    }
  }

  private move(delta: number): void {
    const count = this.options().length;
    if (count === 0) {
      return;
    }

    this.active.update((current) => (current + delta + count) % count);
    this.focusActive();
  }

  private focusActive(): void {
    const element = this.host.nativeElement as HTMLElement;
    const options = element.querySelectorAll<HTMLElement>('.option');
    options.item(this.active())?.focus();
  }

  private focusTrigger(): void {
    const element = this.host.nativeElement as HTMLElement;
    element.querySelector<HTMLButtonElement>('.trigger')?.focus();
  }
}
