import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ActivityEntry } from '../../../core/contracts/dashboard-overview';
import { RecentActivitySection } from './recent-activity';

function entry(index: number): ActivityEntry {
  return {
    id: `activity-${index}`,
    kind: 'healthFailed',
    title: 'Spinner API health failed',
    context: 'Production',
    occurredAt: `2026-08-18T1${index % 10}:00:00.000Z`,
  };
}

describe('RecentActivitySection', () => {
  let fixture: ComponentFixture<RecentActivitySection>;
  let host: HTMLElement;

  async function render(entries: readonly ActivityEntry[]): Promise<void> {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({ imports: [RecentActivitySection] }).compileComponents();

    fixture = TestBed.createComponent(RecentActivitySection);
    fixture.componentRef.setInput('entries', entries);
    await fixture.whenStable();
    host = fixture.nativeElement as HTMLElement;
  }

  it('shows nine events and says how many older ones it left out', async () => {
    await render(Array.from({ length: 14 }, (_, index) => entry(index)));

    // The rail sits beside the surfaces table and is a sample, not the history: it must not imply completeness.
    expect(host.querySelectorAll('.entry').length).toBe(9);
    expect(host.textContent).toContain('5 older not shown');
  });

  it('says nothing about older events when it is showing all of them', async () => {
    await render([entry(1), entry(2)]);

    expect(host.querySelectorAll('.entry').length).toBe(2);
    expect(host.textContent).not.toContain('older not shown');
  });

  it('names the activity screen as planned rather than offering a dead link', async () => {
    await render([entry(1)]);

    const link = host.querySelector('.co-inline-link');
    expect(link?.classList.contains('is-unavailable')).toBe(true);
    expect(link?.getAttribute('title')).toContain('planned');
  });

  it('reports an empty history as empty rather than as nothing having happened', async () => {
    await render([]);

    expect(host.textContent).toContain('No activity recorded yet');
  });
});
