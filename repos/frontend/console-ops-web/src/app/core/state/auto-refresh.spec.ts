import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';

import { autoRefresh } from './auto-refresh';

@Component({ selector: 'co-auto-refresh-host', template: '' })
class AutoRefreshHost {
  readonly reads: number[] = [];

  constructor() {
    autoRefresh(() => this.reads.push(Date.now()), 20);
  }
}

/** Nothing here calls the API: the helper only decides *when* a screen should re-read. */
describe('autoRefresh', () => {
  it('re-reads on the interval while the tab is visible', async () => {
    const fixture = TestBed.createComponent(AutoRefreshHost);
    const host = fixture.componentInstance;

    await new Promise((resolve) => setTimeout(resolve, 70));

    expect(host.reads.length).toBeGreaterThanOrEqual(2);
  });

  it('stops when the component is destroyed', async () => {
    const fixture = TestBed.createComponent(AutoRefreshHost);
    const host = fixture.componentInstance;

    await new Promise((resolve) => setTimeout(resolve, 50));
    const readsBeforeDestroy = host.reads.length;
    fixture.destroy();
    await new Promise((resolve) => setTimeout(resolve, 60));

    expect(host.reads.length).toBe(readsBeforeDestroy);
  });

  it('does not poll a hidden tab, and reads immediately when it becomes visible again', async () => {
    const visibility = vi.spyOn(document, 'visibilityState', 'get');
    visibility.mockReturnValue('hidden');

    const fixture = TestBed.createComponent(AutoRefreshHost);
    const host = fixture.componentInstance;

    await new Promise((resolve) => setTimeout(resolve, 70));
    expect(host.reads.length).toBe(0);

    visibility.mockReturnValue('visible');
    document.dispatchEvent(new Event('visibilitychange'));

    expect(host.reads.length).toBe(1);
    visibility.mockRestore();
    fixture.destroy();
  });
});
