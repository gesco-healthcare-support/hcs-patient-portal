import { TestBed } from '@angular/core/testing';

import { PeopleEditModalComponent } from './people-edit-modal.component';
import type { PeopleSection } from './people.util';

/**
 * Sweep #636. The edit modal had a click-to-dismiss backdrop and no other exit, so a
 * keyboard-only user could open it and be stuck. It had no spec at all.
 *
 * <p>The `busy` guard is the part worth pinning: dismissing a save that is still writing
 * leaves the user looking at a form whose outcome they cannot see. The locations and users
 * hubs guard theirs the same way.</p>
 */
describe('PeopleEditModalComponent Escape handling (sweep #636)', () => {
  interface Probe {
    busy: boolean;
    onEscapeKey(): void;
  }

  function create() {
    TestBed.configureTestingModule({ imports: [PeopleEditModalComponent] });
    const fixture = TestBed.createComponent(PeopleEditModalComponent);
    const cmp = fixture.componentInstance;
    fixture.componentRef.setInput('section', { singular: 'patient' } as unknown as PeopleSection);
    fixture.componentRef.setInput('form', {});
    const cancelled: number[] = [];
    cmp.cancelled.subscribe(() => cancelled.push(1));
    return { fixture, probe: cmp as unknown as Probe, cancelled };
  }

  afterEach(() => TestBed.resetTestingModule());

  it('emits cancelled on Escape', () => {
    const c = create();
    c.probe.onEscapeKey();
    expect(c.cancelled.length).toBe(1);
  });

  it('does not discard a save in flight', () => {
    const c = create();
    c.probe.busy = true;
    c.probe.onEscapeKey();
    expect(c.cancelled.length).toBe(0);
  });

  it('is wired to a real document Escape keypress, not just callable', () => {
    const c = create();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    expect(c.cancelled.length).toBe(1);
  });
});
