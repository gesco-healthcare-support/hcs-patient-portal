import { TestBed } from '@angular/core/testing';
import { SimpleChange } from '@angular/core';
import { of, throwError } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';

import { ApproveConfirmationModalComponent } from './approve-confirmation-modal.component';
import { AppointmentApprovalService } from '../../../proxy/appointments/appointment-approval.service';

/**
 * The staff approve-booking modal: optionally add a comment, approve.
 *
 * <p>Until 2026-11-02 it also made the staff member pick a "Responsible User" from a list fetched
 * when the modal opened, and Approve stayed disabled until one was picked AND the list had loaded.
 * The office retired the responsible user, so THE PART WORTH PINNING is now the absence of that
 * gate: opening the modal fetches nothing, Approve is enabled with nothing filled in, and the
 * request carries only the comment.</p>
 *
 * <p>All names and identifiers below are synthetic.</p>
 */
describe('ApproveConfirmationModalComponent', () => {
  let approval: Record<string, jasmine.Spy>;
  let toaster: { success: jasmine.Spy; error: jasmine.Spy };

  interface Probe {
    [key: string]: any;
  }

  function create(): Probe {
    // Reset first: a test that builds two components configures the module twice, and the
    // second call throws once the first has been instantiated.
    TestBed.resetTestingModule();
    approval = {
      approveAppointment: jasmine
        .createSpy('approveAppointment')
        .and.returnValue(of({ id: 'appt-1', status: 'Approved' })),
    };
    toaster = { success: jasmine.createSpy('success'), error: jasmine.createSpy('error') };

    TestBed.configureTestingModule({
      providers: [
        { provide: AppointmentApprovalService, useValue: approval },
        { provide: ToasterService, useValue: toaster },
      ],
    });

    const c = TestBed.createComponent(ApproveConfirmationModalComponent)
      .componentInstance as unknown as Probe;
    c.appointmentId = 'appt-1';
    return c;
  }

  /** Drive ngOnChanges the way the parent would: a visibility transition. */
  function setVisibility(c: Probe, from: boolean, to: boolean): void {
    c.visible = to;
    c.ngOnChanges({ visible: new SimpleChange(from, to, false) });
  }

  function typeComment(c: Probe, comments: string | null): void {
    c.form.setValue({ internalUserComments: comments });
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('opening', () => {
    it('fetches nothing: there is no list to load any more', () => {
      // The approval service stub has only approveAppointment; a leftover lookup call would throw.
      const c = create();

      setVisibility(c, false, true);

      expect(approval['approveAppointment']).not.toHaveBeenCalled();
      expect(c.approveDisabled)
        .withContext('Approve is ready the moment the modal opens')
        .toBeFalse();
    });

    it('has no responsible-user field', () => {
      const c = create();

      expect(Object.keys(c.form.controls)).toEqual(['internalUserComments']);
    });
  });

  describe('visibility', () => {
    it('announces a change to the parent', () => {
      const c = create();
      const seen: boolean[] = [];
      c.visibleChange.subscribe((v: boolean) => seen.push(v));

      c.setVisible(false);

      expect(seen).toEqual([false]);
      expect(c.visible).toBeFalse();
    });

    it('releases the button when it is closed mid-flight', () => {
      // Otherwise reopening shows a permanently disabled Approve.
      const c = create();
      c.isBusy = true;

      c.setVisible(false);

      expect(c.isBusy).toBeFalse();
      expect(c.approveDisabled).toBeFalse();
    });

    it('leaves the busy flag alone when it is opened', () => {
      const c = create();
      c.isBusy = true;

      c.setVisible(true);

      expect(c.isBusy).toBeTrue();
    });

    it('forgets the comment when it closes', () => {
      const c = create();
      typeComment(c, 'a note');

      setVisibility(c, true, false);

      expect(c.form.getRawValue().internalUserComments).toBeNull();
    });
  });

  describe('the approve gate', () => {
    it('is OPEN with nothing filled in', () => {
      const c = create();

      expect(c.form.valid).toBeTrue();
      expect(c.approveDisabled).toBeFalse();
    });

    it('is shut only while an approval is running', () => {
      const c = create();
      c.isBusy = true;

      expect(c.approveDisabled).toBeTrue();
    });

    it('does nothing without an appointment', () => {
      const c = create();
      c.appointmentId = null;

      c.confirm();

      expect(approval['approveAppointment']).not.toHaveBeenCalled();
    });

    it('does nothing while an approval is already running', () => {
      const c = create();
      c.isBusy = true;

      c.confirm();

      expect(approval['approveAppointment']).not.toHaveBeenCalled();
    });
  });

  describe('approving', () => {
    it('approves with nothing filled in, sending only an empty comment', () => {
      const c = create();

      c.confirm();

      const [id, body] = approval['approveAppointment'].calls.mostRecent().args;
      expect(id).toBe('appt-1');
      expect(body).toEqual({ internalUserComments: null });
    });

    it('trims the comment', () => {
      const c = create();
      typeComment(c, '  looks right  ');

      c.confirm();

      expect(approval['approveAppointment'].calls.mostRecent().args[1]).toEqual({
        internalUserComments: 'looks right',
      });
    });

    it('sends NULL for a whitespace-only comment rather than a blank string', () => {
      // A stored blank reads downstream as "a comment was left", which it was not.
      const c = create();
      typeComment(c, '   ');

      c.confirm();

      expect(
        approval['approveAppointment'].calls.mostRecent().args[1].internalUserComments,
      ).toBeNull();
    });

    it('confirms, hands the approved appointment to the parent, and closes', () => {
      const c = create();
      // OPEN it first. Asserting that a modal closed, when it was never open, passes on a
      // component that does nothing at all -- the same flaw the failure test below had.
      c.visible = true;
      const emitted: unknown[] = [];
      c.succeeded.subscribe((dto: unknown) => emitted.push(dto));

      c.confirm();

      expect(toaster.success).toHaveBeenCalledWith('Appointment booking request has been approved');
      expect(emitted).toEqual([{ id: 'appt-1', status: 'Approved' }]);
      expect(c.visible).toBeFalse();
    });

    it('STAYS OPEN and releases the button when the approval fails', () => {
      /**
       * Closing on failure would hide an appointment that is still pending while the toast
       * said nothing, and the staff member would have to rediscover it in the queue.
       */
      const c = create();
      c.visible = true;
      approval['approveAppointment'].and.returnValue(throwError(() => ({ status: 409 })));

      c.confirm();

      expect(c.visible).withContext('the modal must not close').toBeTrue();
      expect(c.isBusy).toBeFalse();
      expect(toaster.success).not.toHaveBeenCalled();
    });

    it('keeps the comment after a failure so the approval can be retried', () => {
      const c = create();
      typeComment(c, 'a note');
      approval['approveAppointment'].and.returnValue(throwError(() => ({ status: 409 })));

      c.confirm();

      expect(c.form.getRawValue().internalUserComments).toBe('a note');
    });

    it('does not tell the parent anything when the approval fails', () => {
      const c = create();
      approval['approveAppointment'].and.returnValue(throwError(() => ({ status: 409 })));
      const emitted: unknown[] = [];
      c.succeeded.subscribe((dto: unknown) => emitted.push(dto));

      c.confirm();

      expect(emitted).toEqual([]);
    });
  });
});
