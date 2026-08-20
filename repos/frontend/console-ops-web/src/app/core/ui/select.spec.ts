import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Select } from './select';

/**
 * The select that replaced the native one.
 *
 * These exist because replacing a native control means taking on what it did for free. If the keyboard path
 * regresses, the control is worse than the browser default it was built to replace, however it looks.
 */
describe('Select', () => {
  let fixture: ComponentFixture<Select>;
  let host: HTMLElement;

  async function render(value = 'master'): Promise<void> {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({ imports: [Select] }).compileComponents();

    fixture = TestBed.createComponent(Select);
    fixture.componentRef.setInput('options', ['master', 'release/2026-08', 'develop']);
    fixture.componentRef.setInput('value', value);
    fixture.componentRef.setInput('label', 'Branch');
    await fixture.whenStable();
    host = fixture.nativeElement as HTMLElement;
  }

  function trigger(): HTMLButtonElement {
    return host.querySelector<HTMLButtonElement>('.trigger')!;
  }

  function options(): HTMLElement[] {
    return Array.from(host.querySelectorAll<HTMLElement>('.option'));
  }

  /** Keys are handled on the option that has focus, which is the one the list is sitting on. */
  function activeIndex(): number {
    return options().findIndex((option) => option.classList.contains('is-active'));
  }

  async function press(key: string, target: HTMLElement): Promise<void> {
    target.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true }));
    await fixture.whenStable();
  }

  beforeEach(async () => {
    await render();
  });

  it('shows the current value and opens nothing until asked', () => {
    expect(trigger().textContent?.trim()).toBe('master');
    expect(trigger().getAttribute('aria-expanded')).toBe('false');
    expect(options()).toEqual([]);
  });

  it('reports itself as a combobox with its list and name', async () => {
    trigger().click();
    await fixture.whenStable();

    expect(trigger().getAttribute('role')).toBe('combobox');
    expect(trigger().getAttribute('aria-expanded')).toBe('true');
    const list = host.querySelector('.list')!;
    expect(list.getAttribute('role')).toBe('listbox');
    expect(list.id).toBe(trigger().getAttribute('aria-controls'));
    // The visible label sits outside the control, so the control carries its own name.
    expect(list.getAttribute('aria-label')).toBe('Branch');
  });

  it('marks the current option as selected rather than only styling it', async () => {
    trigger().click();
    await fixture.whenStable();

    const selected = options().filter((option) => option.getAttribute('aria-selected') === 'true');
    expect(selected.length).toBe(1);
    expect(selected[0].textContent).toContain('master');
  });

  it('opens on ArrowDown from the trigger and starts on the current value', async () => {
    await press('ArrowDown', trigger());

    expect(options().length).toBe(3);
    expect(options()[0].classList.contains('is-active')).toBe(true);
  });

  it('moves with the arrows, wraps, and jumps with Home and End', async () => {
    trigger().click();
    await fixture.whenStable();

    await press('ArrowDown', options()[activeIndex()]);
    expect(options()[1].classList.contains('is-active')).toBe(true);

    await press('ArrowUp', options()[activeIndex()]);
    await press('ArrowUp', options()[activeIndex()]);
    // Wraps rather than sticking at the top, which is what the native list does.
    expect(options()[2].classList.contains('is-active')).toBe(true);

    await press('Home', options()[activeIndex()]);
    expect(options()[0].classList.contains('is-active')).toBe(true);

    await press('End', options()[activeIndex()]);
    expect(options()[2].classList.contains('is-active')).toBe(true);
  });

  it('chooses with Enter and reports the choice once', async () => {
    const chosen: string[] = [];
    fixture.componentInstance.changed.subscribe((value) => chosen.push(value));

    trigger().click();
    await fixture.whenStable();
    await press('ArrowDown', options()[activeIndex()]);
    await press('Enter', options()[activeIndex()]);

    expect(chosen).toEqual(['release/2026-08']);
    expect(options()).toEqual([]);
  });

  it('closes on Escape without changing anything', async () => {
    const chosen: string[] = [];
    fixture.componentInstance.changed.subscribe((value) => chosen.push(value));

    trigger().click();
    await fixture.whenStable();
    await press('ArrowDown', options()[activeIndex()]);
    await press('Escape', options()[activeIndex()]);

    // Browsing a list is not changing a value: Escape leaves the ref as it was.
    expect(options()).toEqual([]);
    expect(chosen).toEqual([]);
  });

  it('reports nothing when the value chosen is the one already set', async () => {
    const chosen: string[] = [];
    fixture.componentInstance.changed.subscribe((value) => chosen.push(value));

    trigger().click();
    await fixture.whenStable();
    options()[0].click();
    await fixture.whenStable();

    expect(chosen).toEqual([]);
  });

  it('does not open while disabled', async () => {
    fixture.componentRef.setInput('disabled', true);
    await fixture.whenStable();

    trigger().click();
    await fixture.whenStable();

    expect(options()).toEqual([]);
  });
});
