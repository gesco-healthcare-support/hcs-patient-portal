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
    expect(c.cancelled).toHaveSize(1);
  });

  it('does not discard a save in flight', () => {
    const c = create();
    c.probe.busy = true;
    c.probe.onEscapeKey();
    expect(c.cancelled).toHaveSize(0);
  });

  it('is wired to a real document Escape keypress, not just callable', () => {
    const c = create();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    expect(c.cancelled).toHaveSize(1);
  });
});

/**
 * The rest of the modal's own logic: which sections carry an email field, how edits merge into the
 * draft, and the same `busy` guard on Save that Escape has -- a double-click must not send the
 * same save twice.
 */
describe('PeopleEditModalComponent draft and save', () => {
  interface Probe {
    busy: boolean;
    showEmail: boolean;
    draft: () => Record<string, unknown>;
    patch(partial: Record<string, unknown>): void;
    onSave(): void;
  }

  function create(section: Partial<PeopleSection>, form: Record<string, unknown> = {}) {
    TestBed.configureTestingModule({ imports: [PeopleEditModalComponent] });
    const fixture = TestBed.createComponent(PeopleEditModalComponent);
    fixture.componentRef.setInput('section', section as PeopleSection);
    fixture.componentRef.setInput('form', form);
    const saved: unknown[] = [];
    fixture.componentInstance.save.subscribe((value) => saved.push(value));
    return { probe: fixture.componentInstance as unknown as Probe, saved };
  }

  afterEach(() => TestBed.resetTestingModule());

  it('shows the email field for patients', () => {
    expect(create({ key: 'patients', isPatient: true }).probe.showEmail).toBeTrue();
  });

  it('shows the email field for claim examiners', () => {
    expect(create({ key: 'ce', isPatient: false }).probe.showEmail).toBeTrue();
  });

  it('hides the email field for attorneys', () => {
    expect(create({ key: 'aa', isPatient: false }).probe.showEmail).toBeFalse();
  });

  it('merges an edit into the draft without dropping the other fields', () => {
    const { probe } = create({ key: 'aa' }, { firstName: 'Grace', lastName: 'Example' });

    probe.patch({ lastName: 'Sample' });

    expect(probe.draft()).toEqual({ firstName: 'Grace', lastName: 'Sample' });
  });

  it('emits the draft on Save', () => {
    const { probe, saved } = create({ key: 'aa' }, { firstName: 'Grace' });

    probe.onSave();

    expect(saved).toEqual([{ firstName: 'Grace' }]);
  });

  it('does not emit a second save while one is in flight', () => {
    const { probe, saved } = create({ key: 'aa' }, { firstName: 'Grace' });
    probe.busy = true;

    probe.onSave();

    expect(saved).toEqual([]);
  });
});
