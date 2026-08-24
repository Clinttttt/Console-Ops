import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';

import { Icon } from './icon';

/** Tall enough for a handful of branches without becoming a page of its own. */
const MaximumListHeight = 216;

/** Below this the list is not worth opening in the tighter direction. */
const MinimumListHeight = 96;

const ListGap = 4;

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
      <!--
        Positioned against the viewport, not the trigger. Inside a dialog that scrolls, an absolutely positioned
        list is clipped by that scroll container and adds to its scroll height - which is how the options ended up
        cut off behind the panel edge, with the panel growing a scrollbar it did not need.
      -->
      <ul
        class="list"
        role="listbox"
        [attr.id]="listId"
        [attr.aria-label]="label()"
        [style.top.px]="placement().top"
        [style.left.px]="placement().left"
        [style.width.px]="placement().width"
        [style.max-height.px]="placement().maxHeight"
      >
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

  constructor() {
    // A list left open when the dialog around it closes would otherwise keep listening to the window.
    inject(DestroyRef).onDestroy(() => this.close());
  }

  /** Empty rather than a placeholder: the caller always passes a value, and inventing one would be a guess. */
  protected readonly selectedLabel = computed(() => this.value());

  /** Where the list sits in the viewport, measured from the trigger each time it opens. */
  protected readonly placement = signal({
    top: 0,
    left: 0,
    width: 0,
    maxHeight: MaximumListHeight,
  });

  protected toggle(): void {
    if (this.disabled()) {
      return;
    }

    if (this.open()) {
      this.close();
      return;
    }

    this.expand();
  }

  private expand(): void {
    this.measure();
    this.open.set(true);
    this.active.set(Math.max(0, this.options().indexOf(this.value())));

    // Kept correct while open rather than closed on the first scroll: an operator reading a long list inside a
    // dialog scrolls it, and losing their place there would be its own annoyance.
    window.addEventListener('scroll', this.reposition, true);
    window.addEventListener('resize', this.reposition);

    // Focus moves into the list so the arrow keys land there rather than scrolling what is behind it.
    queueMicrotask(() => this.focusActive());
  }

  private close(): void {
    this.open.set(false);
    window.removeEventListener('scroll', this.reposition, true);
    window.removeEventListener('resize', this.reposition);
  }

  private readonly reposition = (): void => this.measure();

  /**
   * Measures the trigger and decides which side of it the list opens on.
   *
   * Below when there is room, above when there is not, and never taller than the space it has - a list that runs
   * off the bottom of the window cannot be reached with a pointer.
   */
  private measure(): void {
    const element = this.host.nativeElement as HTMLElement;
    const trigger = element.querySelector<HTMLElement>('.trigger');
    if (trigger === null) {
      return;
    }

    const rect = trigger.getBoundingClientRect();
    const below = window.innerHeight - rect.bottom - ListGap;
    const above = rect.top - ListGap;
    const opensUp = below < Math.min(MaximumListHeight, above);
    const room = Math.max(MinimumListHeight, Math.min(MaximumListHeight, opensUp ? above : below));

    this.placement.set({
      top: opensUp ? Math.max(ListGap, rect.top - room - ListGap) : rect.bottom + ListGap,
      left: rect.left,
      width: rect.width,
      maxHeight: room,
    });
  }

  protected choose(option: string): void {
    this.close();
    this.focusTrigger();

    if (option !== this.value()) {
      this.changed.emit(option);
    }
  }

  protected onTriggerKey(event: KeyboardEvent): void {
    if (event.key === 'ArrowDown' || event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      if (!this.open()) {
        this.expand();
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
        this.close();
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
