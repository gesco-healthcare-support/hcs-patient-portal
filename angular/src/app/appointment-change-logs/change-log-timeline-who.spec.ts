import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { ChangeLogTimelineComponent } from './change-log-timeline.component';
import type { ChangeLogRow } from './clg-log.util';

/**
 * Renders the shared timeline with the who / action fields and asserts the DOM, because the label and
 * the name are the whole point of the change and a class-level probe cannot see a template.
 */
describe('ChangeLogTimelineComponent who and action', () => {
  const base: ChangeLogRow = {
    appointmentId: 'appt-1',
    entityType: 'Appointment',
    changeType: 'Updated',
    changeTime: '2026-01-02T10:00:00Z',
    propertyName: 'AppointmentStatus',
    oldValue: '1',
    newValue: '2',
    valueRedacted: false,
  };

  const staffRow: ChangeLogRow = {
    ...base,
    actionLabel: 'Approved',
    changedByName: 'Sam Supervisor',
    changedByKind: 'Staff',
  };
  const hostRow: ChangeLogRow = {
    ...base,
    changeTime: '2026-01-03T10:00:00Z',
    actionLabel: 'Rejected',
    changedByName: 'Pat Operator',
    changedByKind: 'HostStaff',
  };
  const outsideRow: ChangeLogRow = {
    ...base,
    changeTime: '2026-01-04T10:00:00Z',
    actionLabel: 'Resubmitted',
    changedByName: 'Lee Lawyer',
    changedByKind: 'Outside',
  };
  const systemRow: ChangeLogRow = {
    ...base,
    changeTime: '2026-01-05T10:00:00Z',
    actionLabel: 'No show',
    changedByName: 'Case Tracker',
    changedByKind: 'CaseTracker',
  };
  const eventRow: ChangeLogRow = {
    appointmentId: 'appt-1',
    entityType: 'Authorized User',
    changeType: 'Created',
    changeTime: '2026-01-06T10:00:00Z',
    propertyName: '',
    valueRedacted: false,
    actionLabel: 'Authorized user added',
    changedByName: 'Sam Supervisor',
    changedByKind: 'Staff',
  };
  const legacyRow: ChangeLogRow = { ...base, changeTime: '2026-01-07T10:00:00Z' };

  const all = [staffRow, hostRow, outsideRow, systemRow, eventRow, legacyRow];

  function render(rows: ChangeLogRow[]): HTMLElement {
    TestBed.configureTestingModule({
      imports: [ChangeLogTimelineComponent],
      providers: [{ provide: Router, useValue: { navigateByUrl: () => Promise.resolve(true) } }],
    });
    const fixture = TestBed.createComponent(ChangeLogTimelineComponent);
    fixture.componentRef.setInput('rows', rows);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows the action label and who made each change', () => {
    const el = render(all);
    const text = el.textContent ?? '';
    expect(text).toContain('Approved');
    expect(text).toContain('Sam Supervisor');
    expect(text).toContain('Authorized user added');
    expect(el.querySelectorAll('.clg-action').length).toBe(5);
  });

  it('hints host staff and outside people but not office staff', () => {
    const el = render(all);
    const whos = Array.from(el.querySelectorAll('.clg-who')).map((n) => n.textContent ?? '');
    expect(whos.find((t) => t.includes('Pat Operator'))).toContain('(host staff)');
    expect(whos.find((t) => t.includes('Lee Lawyer'))).toContain('(outside)');
    expect(whos.find((t) => t.includes('Sam Supervisor') && !t.includes('('))).toBeTruthy();
  });

  it('renders system actors muted and without a hint', () => {
    const el = render(all);
    const muted = Array.from(el.querySelectorAll('.clg-who--muted')).map(
      (n) => n.textContent ?? '',
    );
    expect(muted.length).toBe(1);
    expect(muted[0]).toContain('Case Tracker');
    expect(muted[0]).not.toContain('(');
  });

  it('counts only rows that have a field name, so an event reads 0 fields', () => {
    const el = render([eventRow]);
    expect(el.querySelector('.cnt')?.textContent?.trim()).toBe('0 fields');
  });

  it('falls back to the old entity-updated headline when the server sends no action', () => {
    const el = render([legacyRow]);
    expect(el.querySelector('.clg-action')).toBeNull();
    expect(el.textContent).toContain('Appointment updated');
    expect(el.querySelector('.clg-who')).toBeNull();
  });
});
