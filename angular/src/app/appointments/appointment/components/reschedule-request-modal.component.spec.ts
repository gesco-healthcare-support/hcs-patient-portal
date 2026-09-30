import { TestBed } from '@angular/core/testing';
import { SimpleChange } from '@angular/core';
import { of, throwError } from 'rxjs';

import { RescheduleRequestModalComponent } from './reschedule-request-modal.component';
import { AppointmentChangeRequestService } from '../../../proxy/appointment-change-requests/appointment-change-request.service';

/**
 * The reschedule-request modal, filed either by staff or by an external party.
 *
 * <p>It had NO spec of its own. lcov showed 16 of 48 lines already reached -- incidentally,
 * through the detail page that hosts it -- leaving 32 uncovered, which is what this adds.</p>
 *
 * <p>Same idiom as `request-info-modal` and `approve-confirmation-modal`, with one structural
 * difference worth stating: THE TWO FILERS SUBMIT DIFFERENT THINGS. Staff pick a slot and send
 * it; an external party proposes nothing and staff choose the date at approval. So a null slot
 * is a valid submission here, not a missing field, and the gate has to tell those apart.</p>
 *
 * <p>`modalOptions` is not an incidental getter. Its own source says a fresh object literal
 * there hangs the browser -- an ABP signal input re-reads it on every change-detection pass, and
 * a new object each time never settles. It delegates to a helper returning frozen constants, and
 * the test below asserts IDENTITY rather than equality, because equality would pass on exactly
 * the shape that hangs.</p>
 *
 * <p>All identifiers and reasons below are synthetic.</p>
 */
describe('RescheduleRequestModalComponent', () => {
  let changeRequests: Record<string, jasmine.Spy>;

  interface Probe {
    [key: string]: any;
  }

  const SLOT = {
    date: '2026-10-01',
    time: '09:00',
    doctorAvailabilityId: 'slot-1',
  };

  function create(requesterIsStaff = true): Probe {
    // Reset first: the tests that compare a staff filer against an external one build two
    // components, and configuring the module twice throws once the first is instantiated.
    TestBed.resetTestingModule();
    changeRequests = {
      requestReschedule: jasmine
        .createSpy('requestReschedule')
        .and.returnValue(of({ id: 'cr-1', status: 'Pending' })),
    };

    TestBed.configureTestingModule({
      providers: [{ provide: AppointmentChangeRequestService, useValue: changeRequests }],
    });

    const c = TestBed.createComponent(RescheduleRequestModalComponent)
      .componentInstance as unknown as Probe;
    c.appointmentId = 'appt-1';
    c.requesterIsStaff = requesterIsStaff;
    return c;
  }

  /**
   * A staff filer with the modal OPEN, a slot chosen and a reason typed -- the submittable
   * state.
   *
   * <p>`visible` is set deliberately. A test asserting the dialog "stays open" after a failed
   * submit passes trivially against a dialog that was never open, and would pass against a
   * component that does nothing at all.</p>
   */
  function readyStaff(): Probe {
    const c = create(true);
    c.visible = true;
    c.onSlotSelected(SLOT);
    c.reason = 'Doctor unavailable that morning.';
    return c;
  }

  function setVisibility(c: Probe, from: boolean, to: boolean): void {
    c.visible = to;
    c.ngOnChanges({ visible: new SimpleChange(from, to, false) });
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('the dialog options', () => {
    it('returns the SAME object every read, not a fresh literal', () => {
      /**
       * Identity, deliberately. An equal-but-new object on each read is exactly the shape
       * the source warns hangs change detection, and `toEqual` would pass on it.
       */
      const c = create(true);

      expect(c.modalOptions).toBe(c.modalOptions);
    });

    it('gives staff a different dialog from an external filer', () => {
      // Staff get the wide dialog so the two-month datepicker is not clipped.
      const staff = create(true);
      const external = create(false);

      expect(staff.modalOptions).not.toBe(external.modalOptions);
    });

    it('states the booking rule in words the filer can act on', () => {
      const c = create(true);
      expect(c.minimumBookingRuleMessage).toBe('Appointments must be at least 3 days from today.');
    });
  });

  describe('choosing a slot', () => {
    it('mirrors the date, time and resolved slot id', () => {
      const c = create(true);

      c.onSlotSelected(SLOT);

      expect(c.selectedDate).toBe('2026-10-01');
      expect(c.selectedTime).toBe('09:00');
      expect(c.newDoctorAvailabilityId).toBe('slot-1');
    });

    it("takes the calendar's first emission, which carries a date but no time yet", () => {
      /**
       * The calendar emits the date alone first so a parent can intercept before a slot is
       * committed, then again with the time and slot id. Submit must not unlock on the first.
       */
      const c = create(true);
      c.reason = 'Needs a later slot.';

      c.onSlotSelected({ date: '2026-10-01', time: null, doctorAvailabilityId: null });

      expect(c.selectedDate).toBe('2026-10-01');
      expect(c.canSubmit).withContext('no real slot identified yet').toBeFalse();
    });

    it('clears all three when the date is cleared', () => {
      const c = create(true);
      c.onSlotSelected(SLOT);

      c.onDateCleared();

      expect(c.selectedDate).toBeNull();
      expect(c.selectedTime).toBeNull();
      expect(c.newDoctorAvailabilityId).toBeNull();
    });
  });

  describe('the submit gate', () => {
    it('opens for staff once a slot and a reason are present', () => {
      const c = readyStaff();
      expect(c.canSubmit).toBeTrue();
    });

    it('stays shut for staff with a reason but no slot', () => {
      const c = create(true);
      c.reason = 'Needs a later slot.';
      expect(c.canSubmit).toBeFalse();
    });

    it('opens for an EXTERNAL filer with a reason and no slot at all', () => {
      // The asymmetry that makes this modal two modals: external parties propose no date.
      const c = create(false);
      c.reason = 'I am unavailable that week.';

      expect(c.canSubmit).toBeTrue();
    });

    it('stays shut without a reason, for either filer', () => {
      const staff = create(true);
      staff.onSlotSelected(SLOT);
      expect(staff.canSubmit).toBeFalse();

      const external = create(false);
      expect(external.canSubmit).toBeFalse();
    });

    it('stays shut for a reason longer than the stored column', () => {
      const c = create(false);
      c.reason = 'a'.repeat(c.maxReasonLength + 1);
      expect(c.canSubmit).toBeFalse();
    });

    it('SHUTS while a submission is already running', () => {
      const c = readyStaff();
      c.isBusy = true;
      expect(c.canSubmit).toBeFalse();
    });
  });

  describe('submitting', () => {
    it('sends the chosen slot, the trimmed reason and the appointment id', () => {
      const c = readyStaff();
      c.reason = '  Doctor unavailable.  ';

      c.submit();

      const [id, body] = changeRequests['requestReschedule'].calls.mostRecent().args;
      expect(id).toBe('appt-1');
      expect(body.newDoctorAvailabilityId).toBe('slot-1');
      expect(body.reScheduleReason).toBe('Doctor unavailable.');
      expect(body.isBeyondLimit).toBeFalse();
    });

    it('sends a NULL slot for an external filer, which staff fill in at approval', () => {
      const c = create(false);
      c.reason = 'I am unavailable that week.';

      c.submit();

      expect(
        changeRequests['requestReschedule'].calls.mostRecent().args[1].newDoctorAvailabilityId,
      ).toBeNull();
    });

    it('does nothing without an appointment', () => {
      const c = readyStaff();
      c.appointmentId = null;

      c.submit();

      expect(changeRequests['requestReschedule']).not.toHaveBeenCalled();
    });

    it('does nothing while the gate is shut', () => {
      const c = create(true);

      c.submit();

      expect(changeRequests['requestReschedule']).not.toHaveBeenCalled();
    });

    it('hands the created request to the parent and closes', () => {
      const c = readyStaff();
      const emitted: unknown[] = [];
      c.succeeded.subscribe((dto: unknown) => emitted.push(dto));

      c.submit();

      expect(emitted).toEqual([{ id: 'cr-1', status: 'Pending' }]);
      expect(c.visible).toBeFalse();
    });

    it('clears any earlier error when a new attempt starts', () => {
      const c = readyStaff();
      c.errorMessage = 'stale message';

      c.submit();

      expect(c.errorMessage).toBeNull();
    });
  });

  describe('when the submission fails', () => {
    it("shows the SERVER's message, which names the actual problem", () => {
      /**
       * An unmapped BusinessException -- NewSlotNotAvailable, when the chosen slot fills
       * between picking and submitting -- otherwise reaches only ABP's generic dialog, and
       * the filer is told nothing about which slot or why.
       */
      const c = readyStaff();
      changeRequests['requestReschedule'].and.returnValue(
        throwError(() => ({ error: { error: { message: 'That slot is no longer available.' } } })),
      );

      c.submit();

      expect(c.errorMessage).toBe('That slot is no longer available.');
    });

    it('falls back to guidance when the server sends no message', () => {
      const c = readyStaff();
      changeRequests['requestReschedule'].and.returnValue(throwError(() => ({ status: 500 })));

      c.submit();

      expect(c.errorMessage).toContain('could not be submitted');
    });

    it('survives an error shaped nothing like the expected envelope', () => {
      // The optional chaining is load-bearing: a network failure is not an ABP error body,
      // and reading through it must not throw inside the error handler itself.
      const c = readyStaff();
      changeRequests['requestReschedule'].and.returnValue(throwError(() => null));

      c.submit();

      expect(c.errorMessage).toContain('could not be submitted');
    });

    it('STAYS OPEN and releases the button, so Submit and Escape work again', () => {
      const c = readyStaff();
      changeRequests['requestReschedule'].and.returnValue(throwError(() => ({ status: 409 })));

      c.submit();

      expect(c.visible).withContext('the dialog stays dismissible, not closed for you').toBeTrue();
      expect(c.isBusy).toBeFalse();
    });

    it('PRESERVES the slot and the reason for a retry', () => {
      // Clearing them would make the filer re-pick a slot and retype the reason to try again.
      const c = readyStaff();
      changeRequests['requestReschedule'].and.returnValue(throwError(() => ({ status: 409 })));

      c.submit();

      expect(c.newDoctorAvailabilityId).toBe('slot-1');
      expect(c.reason).toBe('Doctor unavailable that morning.');
      expect(c.canSubmit).withContext('retry is possible straight away').toBeTrue();
    });

    it('tells the parent nothing', () => {
      const c = readyStaff();
      changeRequests['requestReschedule'].and.returnValue(throwError(() => ({ status: 409 })));
      const emitted: unknown[] = [];
      c.succeeded.subscribe((dto: unknown) => emitted.push(dto));

      c.submit();

      expect(emitted).toEqual([]);
    });
  });

  describe('opening and closing', () => {
    it('resets the form when the modal OPENS', () => {
      // Reopening after an abandoned attempt must not show the previous reason.
      const c = readyStaff();
      c.errorMessage = 'stale';

      setVisibility(c, false, true);

      expect(c.reason).toBe('');
      expect(c.newDoctorAvailabilityId).toBeNull();
      expect(c.selectedDate).toBeNull();
      expect(c.errorMessage).toBeNull();
    });

    it('does NOT reset on a change where it was already open', () => {
      /**
       * The guard reads previousValue, not just the current one. Without it, any parent
       * re-binding while the modal is open wipes what the user has typed.
       */
      const c = readyStaff();

      c.visible = true;
      c.ngOnChanges({ visible: new SimpleChange(true, true, false) });

      expect(c.reason).toBe('Doctor unavailable that morning.');
    });

    it('ignores changes that are not about visibility', () => {
      const c = readyStaff();

      c.ngOnChanges({ locationId: new SimpleChange(null, 'loc-2', false) });

      expect(c.reason).toBe('Doctor unavailable that morning.');
    });

    it('resets and releases the button when closed from inside', () => {
      const c = readyStaff();
      c.isBusy = true;
      const seen: boolean[] = [];
      c.visibleChange.subscribe((v: boolean) => seen.push(v));

      c.setVisible(false);

      expect(seen).toEqual([false]);
      expect(c.reason).toBe('');
      expect(c.isBusy).toBeFalse();
    });

    it('announces an open without wiping the form', () => {
      const c = readyStaff();
      const seen: boolean[] = [];
      c.visibleChange.subscribe((v: boolean) => seen.push(v));

      c.setVisible(true);

      expect(seen).toEqual([true]);
      expect(c.reason).toBe('Doctor unavailable that morning.');
    });
  });
});
