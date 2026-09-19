import { ComponentFixture, TestBed } from '@angular/core/testing';

import { InfoRequestHistoryComponent } from './info-request-history.component';
import type { AppointmentInfoRequestRoundDto } from '../../../proxy/appointment-info-requests/models';

describe('InfoRequestHistoryComponent', () => {
  let fixture: ComponentFixture<InfoRequestHistoryComponent>;
  let component: InfoRequestHistoryComponent;

  // Synthetic round: a resolved Send Back with one changed field (DOB) and one
  // unchanged flagged field (address). All values are fabricated test data.
  const resolvedRound: AppointmentInfoRequestRoundDto = {
    id: '11111111-1111-1111-1111-111111111111',
    roundNumber: 1,
    note: 'Please correct the date of birth.',
    requestedByName: 'Dana Staff',
    requestedAt: '2026-06-10T16:00:00Z',
    isResolved: true,
    resolvedAt: '2026-06-11T16:00:00Z',
    resubmittedByName: 'Avery Attorney',
    flaggedCount: 2,
    fixedCount: 1,
    diffs: [
      { key: 'dateOfBirth', oldValue: '01/01/1980', newValue: '02/02/1980', changed: true },
      { key: 'address', oldValue: '1 A St', newValue: '1 A St', changed: false },
    ],
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [InfoRequestHistoryComponent],
    }).compileComponents();
    fixture = TestBed.createComponent(InfoRequestHistoryComponent);
    component = fixture.componentInstance;
  });

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  function diffCount(): number {
    return (fixture.nativeElement as HTMLElement).querySelectorAll('.irh-diff').length;
  }

  it('renders nothing when there are no rounds', () => {
    component.rounds = [];
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('.irh-round')).toBeNull();
  });

  it('internal view renders only the changed field diffs with old and new values', () => {
    component.rounds = [resolvedRound];
    component.externalView = false;
    fixture.detectChanges();

    expect(diffCount()).toBe(1); // only the changed DOB row, not the unchanged address
    const body = text();
    expect(body).toContain('01/01/1980');
    expect(body).toContain('02/02/1980');
    expect(body).toContain('Dana Staff'); // real requester name shown internally
  });

  it('external view hides diffs, genericises the staff requester, and shows the count summary', () => {
    component.rounds = [resolvedRound];
    component.externalView = true;
    fixture.detectChanges();

    expect(diffCount()).toBe(0); // no field-level values in the external view
    const body = text();
    expect(body).toContain('HCS staff'); // requester genericised
    expect(body).not.toContain('Dana Staff'); // real staff name not leaked externally
    expect(body).not.toContain('01/01/1980'); // no field values leaked externally
    expect(body).toContain('1 of 2 flagged items fixed'); // count summary still shown
  });
});
