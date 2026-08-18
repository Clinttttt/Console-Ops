import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

/** Accessible on/off switch. Uses a real checkbox so keyboard and screen-reader behaviour is free. */
@Component({
  selector: 'co-toggle',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: inline-flex;
    }

    label {
      display: inline-flex;
      align-items: center;
      cursor: pointer;
    }

    input {
      position: absolute;
      opacity: 0;
      width: 0;
      height: 0;
    }

    .track {
      position: relative;
      width: 34px;
      height: 19px;
      border-radius: 999px;
      background: var(--co-line-strong);
      transition: background 120ms ease;
    }

    .knob {
      position: absolute;
      top: 2.5px;
      left: 2.5px;
      width: 14px;
      height: 14px;
      border-radius: 50%;
      background: #fff;
      transition: transform 120ms ease;
    }

    input:checked + .track {
      background: var(--co-blue);
    }

    input:checked + .track .knob {
      transform: translateX(15px);
    }

    input:focus-visible + .track {
      outline: 2px solid var(--co-navy-soft);
      outline-offset: 2px;
    }
  `,
  template: `
    <label>
      <input
        type="checkbox"
        [checked]="checked()"
        [attr.aria-label]="label()"
        (change)="checkedChange.emit($any($event.target).checked)"
      />
      <span class="track"><span class="knob"></span></span>
    </label>
  `,
})
export class Toggle {
  readonly checked = input(false);
  readonly label = input.required<string>();
  readonly checkedChange = output<boolean>();
}
