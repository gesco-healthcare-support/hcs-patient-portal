import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AppointmentChangeLogService } from '../../proxy/appointment-change-logs';
import { AppointmentInfoRequestService } from '../../proxy/appointment-info-requests/appointment-info-request.service';
import { AppointmentChangeLogsComponent } from './appointment-change-logs.component';

/**
 * The per-appointment change-log page loads TWO independent sources: the PHI-redacted audit
 * projection and the Send Back / request-info rounds.
 *
 * The component states the guarantee in a comment at :82 -- "one failing does not blank the
 * other" -- and nothing pinned it until now. That independence is the highest-value assertion
 * in this file: it is a promise about behaviour under partial failure, which is exactly the
 * kind of promise that rots silently, because both sources succeed in every manual test.
 */
describe('AppointmentChangeLogsComponent', () => {
  let getByAppointment: jasmine.Spy;
  let getHistory: jasmine.Spy;
  let navigate: jasmine.Spy;
  let routeId: string | null;

  const auditRow = {
    appointmentId: 'appt-1',
    entityType: 'Appointment',
    propertyName: 'AppointmentDate',
    oldValue: '2026-01-01',
    newValue: '2026-01-02',
    valueRedacted: false,
    changeType: 'Updated',
    changeTime: '2026-01-02T10:00:00',
  };
  const round = { roundNumber: 1, requestedAt: '2026-01-03T09:00:00' };

  beforeEach(() => {
    routeId = 'appt-1';
    getByAppointment = jasmine.createSpy('getByAppointment').and.returnValue(of([auditRow]));
    getHistory = jasmine.createSpy('getHistory').and.returnValue(of([round]));
    navigate = jasmine.createSpy('navigate').and.returnValue(Promise.resolve(true));

    TestBed.configureTestingModule({
      imports: [AppointmentChangeLogsComponent],
      providers: [
        {
          // Keyed on the parameter NAME rather than returning the id for anything asked.
          // A stub that ignores its argument would answer a component reading the wrong
          // parameter just as happily, so nothing would pin that it reads 'id'.
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: { get: (key: string) => (key === 'id' ? routeId : null) } },
          },
        },
        { provide: AppointmentChangeLogService, useValue: { getByAppointment } },
        { provide: AppointmentInfoRequestService, useValue: { getHistory } },
        { provide: Router, useValue: { navigate } },
      ],
    });
  });

  function make(): AppointmentChangeLogsComponent {
    return TestBed.createComponent(AppointmentChangeLogsComponent).componentInstance;
  }

  describe('with no appointment id on the route', () => {
    beforeEach(() => {
      routeId = null;
    });

    it('reports the missing id and stops loading', () => {
      const cmp = make();
      cmp.ngOnInit();
      expect(cmp.errorMessage).toBe('No appointment id provided.');
      expect(cmp.isLoading).toBeFalse();
    });

    it('calls NEITHER service', () => {
      make().ngOnInit();
      expect(getByAppointment).not.toHaveBeenCalled();
      expect(getHistory).not.toHaveBeenCalled();
    });
  });

  // Positive control for the pair above: with an id present, both services ARE called. The
  // two negatives are only meaningful beside this.
  it('calls both services with the route id when one is present', () => {
    make().ngOnInit();
    expect(getByAppointment).toHaveBeenCalledWith('appt-1');
    expect(getHistory).toHaveBeenCalledWith('appt-1');
  });

  describe('the audit timeline', () => {
    it('publishes the returned entries and stops loading', () => {
      const cmp = make();
      cmp.ngOnInit();
      expect(cmp.entries).toEqual([auditRow]);
      expect(cmp.isLoading).toBeFalse();
      expect(cmp.appointmentId).toBe('appt-1');
    });

    it('treats a null response as empty', () => {
      getByAppointment.and.returnValue(of(null));
      const cmp = make();
      cmp.ngOnInit();
      expect(cmp.entries).toEqual([]);
    });

    it('reports a failure and stops loading', () => {
      getByAppointment.and.returnValue(throwError(() => new Error('boom')));
      const cmp = make();
      cmp.ngOnInit();
      expect(cmp.errorMessage).toBe('Failed to load change log.');
      expect(cmp.isLoading).toBeFalse();
    });
  });

  describe('the request-info rounds', () => {
    it('publishes the returned rounds', () => {
      const cmp = make();
      cmp.ngOnInit();
      expect(cmp.rounds).toEqual([round]);
      expect(cmp.roundsError).toBeFalse();
    });

    it('treats a null response as empty', () => {
      getHistory.and.returnValue(of(null));
      const cmp = make();
      cmp.ngOnInit();
      expect(cmp.rounds).toEqual([]);
    });

    it('flags a failure without an error message of its own', () => {
      getHistory.and.returnValue(throwError(() => new Error('boom')));
      const cmp = make();
      cmp.ngOnInit();
      expect(cmp.roundsError).toBeTrue();
    });
  });

  describe('the two sources are independent (the guarantee at :82)', () => {
    it('keeps the audit timeline when the rounds fail', () => {
      getHistory.and.returnValue(throwError(() => new Error('boom')));
      const cmp = make();
      cmp.ngOnInit();
      expect(cmp.roundsError).toBeTrue();
      expect(cmp.entries).toEqual([auditRow]);
      expect(cmp.errorMessage).toBe('');
    });

    it('keeps the rounds when the audit timeline fails', () => {
      getByAppointment.and.returnValue(throwError(() => new Error('boom')));
      const cmp = make();
      cmp.ngOnInit();
      expect(cmp.errorMessage).toBe('Failed to load change log.');
      expect(cmp.rounds).toEqual([round]);
      expect(cmp.roundsError).toBeFalse();
    });
  });

  describe('back', () => {
    it('returns to the appointment it was opened from', () => {
      const cmp = make();
      cmp.ngOnInit();
      cmp.back();
      expect(navigate).toHaveBeenCalledWith(['/appointments/view', 'appt-1']);
    });

    it('falls back to the appointment list when there is no id', () => {
      const cmp = make();
      cmp.back();
      expect(navigate).toHaveBeenCalledWith(['/appointments']);
    });
  });
});
