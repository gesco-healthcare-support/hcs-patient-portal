import { TestBed } from '@angular/core/testing';
import { SimpleChange } from '@angular/core';
import { of, throwError } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';

import { ApproveConfirmationModalComponent } from './approve-confirmation-modal.component';
import { AppointmentApprovalService } from '../../../proxy/appointments/appointment-approval.service';

/**
 * The staff approve-booking modal: pick a primary responsible user, optionally add a comment,
 * approve.
 *
 * <p>It had NO spec and sat at 1 of 40 lines covered.</p>
 *
 * <p>Same idiom as `request-info-modal` and `reschedule-request-modal`: inputs for the
 * appointment and visibility, outputs for visibility and success, an injected service, and a
 * `firstValueFrom` inside a real try/catch -- so unlike the profile pages in the previous
 * tranche, every failure path here is honestly testable and none is excluded.</p>
 *
 * <p>THE PART WORTH PINNING is the lazy load. The responsible-user list is fetched the first
 * time the modal becomes visible and re-fetched on each reopen, because the staff list changes
 * underneath it. Two things can go wrong quietly: fetching on every change-detection pass
 * instead of on the open, and leaving `isLoadingUsers` set after a failure, which turns the
 * retry button into a no-op forever.</p>
 *
 * <p>All names and identifiers below are synthetic.</p>
 */
describe('ApproveConfirmationModalComponent', () => {
  let approval: Record<string, jasmine.Spy>;
  let toaster: { success: jasmine.Spy; error: jasmine.Spy };

  interface Probe {
    [key: string]: any;
  }

  const USERS = [
    { id: 'u-1', displayName: 'Sam Reed' },
    { id: 'u-2', displayName: 'Ada Lovelace' },
  ];

  function create(): Probe {
    // Reset first: a test that builds two components configures the module twice, and the
    // second call throws once the first has been instantiated.
    TestBed.resetTestingModule();
    approval = {
      getInternalUserLookup: jasmine
        .createSpy('getInternalUserLookup')
        .and.returnValue(of({ items: USERS })),
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

  /**
   * Let the lookup settle.
   *
   * <p>`loadResponsibleUsers` is an ASYNC method awaiting `firstValueFrom`, so even a
   * synchronous `of(...)` stub only lands on a later microtask. Asserting straight after
   * `ngOnChanges` reads the state as it was BEFORE the load -- an empty list and the loading
   * flag still set -- which is what six of these tests did on the first run.</p>
   *
   * <p>A timeout rather than a counted number of `Promise.resolve()` hops: it drains every
   * pending microtask whatever the chain length, so the tests do not encode how many awaits
   * the implementation happens to use today.</p>
   */
  function settle(): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, 0));
  }

  /** A form the component would accept. */
  function fillForm(c: Probe, comments: string | null = null): void {
    c.form.setValue({ primaryResponsibleUserId: 'u-1', internalUserComments: comments });
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('loading the responsible-user list', () => {
    it('fetches when the modal OPENS, not before', async () => {
      const c = create();
      expect(approval['getInternalUserLookup'])
        .withContext('nothing fetched while closed')
        .not.toHaveBeenCalled();

      setVisibility(c, false, true);
      await settle();

      expect(approval['getInternalUserLookup']).toHaveBeenCalledTimes(1);
      expect(c.responsibleUsers).toEqual(USERS);
    });

    it('RE-FETCHES on reopen, because the staff list moves underneath it', async () => {
      /**
       * Caching would show a leaver as assignable, or hide a new joiner, until reload.
       *
       * Each open is settled before the next: the open path is guarded on isLoadingUsers,
       * so stacking the transitions without letting the first finish makes the second a
       * no-op and the test would be measuring the guard rather than the re-fetch.
       */
      const c = create();

      setVisibility(c, false, true);
      await settle();
      setVisibility(c, true, false);
      setVisibility(c, false, true);
      await settle();

      expect(approval['getInternalUserLookup']).toHaveBeenCalledTimes(2);
    });

    it('does not fetch on a change that is not about visibility', () => {
      const c = create();

      c.ngOnChanges({ appointmentId: new SimpleChange(null, 'appt-2', false) });

      expect(approval['getInternalUserLookup']).not.toHaveBeenCalled();
    });

    it('asks for the whole list in one page', () => {
      const c = create();
      setVisibility(c, false, true);

      const [query] = approval['getInternalUserLookup'].calls.mostRecent().args;
      expect(query.skipCount).toBe(0);
      expect(query.maxResultCount).toBe(100);
      expect(query.filter).toBe('');
    });

    it('treats a page with no items as an empty list', async () => {
      const c = create();
      approval['getInternalUserLookup'].and.returnValue(of({}));

      setVisibility(c, false, true);
      await settle();

      expect(c.responsibleUsers).toEqual([]);
    });

    it('clears the loading flag on success', async () => {
      const c = create();
      setVisibility(c, false, true);
      await settle();
      expect(c.isLoadingUsers).toBeFalse();
    });
  });

  describe('when the list cannot be loaded', () => {
    it('shows a retryable message and empties the list', async () => {
      const c = create();
      approval['getInternalUserLookup'].and.returnValue(throwError(() => ({ status: 500 })));

      setVisibility(c, false, true);
      await settle();

      expect(c.loadError).toBe('Failed to load responsible-user list. Please retry.');
      expect(c.responsibleUsers).toEqual([]);
    });

    it('CLEARS the loading flag after a failure, so retry is not a dead button', async () => {
      /**
       * The flag also guards the open path. Left set, the modal never fetches again for the
       * life of the page, and the retry button does nothing at all.
       */
      const c = create();
      approval['getInternalUserLookup'].and.returnValue(throwError(() => ({ status: 500 })));

      setVisibility(c, false, true);
      await settle();

      expect(c.isLoadingUsers).toBeFalse();
    });

    it('recovers on retry once the server is back', async () => {
      // The positive control for the two tests above: the failure state is not terminal.
      const c = create();
      approval['getInternalUserLookup'].and.returnValue(throwError(() => ({ status: 500 })));
      setVisibility(c, false, true);
      await settle();

      approval['getInternalUserLookup'].and.returnValue(of({ items: USERS }));
      c.retryLoad();
      await settle();

      expect(c.loadError).toBeNull();
      expect(c.responsibleUsers).toEqual(USERS);
    });

    it('drops a stale error when the modal is closed', async () => {
      const c = create();
      approval['getInternalUserLookup'].and.returnValue(throwError(() => ({ status: 500 })));
      setVisibility(c, false, true);
      await settle();

      setVisibility(c, true, false);

      expect(c.loadError).withContext('a reopened modal starts clean').toBeNull();
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
    });

    it('leaves the busy flag alone when it is opened', () => {
      const c = create();
      c.isBusy = true;

      c.setVisible(true);

      expect(c.isBusy).toBeTrue();
    });

    it('resets the form when it closes', () => {
      const c = create();
      fillForm(c, 'a note');

      setVisibility(c, true, false);

      expect(c.form.getRawValue().primaryResponsibleUserId).toBeNull();
      expect(c.form.getRawValue().internalUserComments).toBeNull();
    });
  });

  describe('the approve gate', () => {
    it('requires a responsible user', () => {
      // The one genuinely required field: an approved appointment with nobody responsible
      // for it has no owner downstream.
      const c = create();
      expect(c.form.invalid).toBeTrue();

      fillForm(c);

      expect(c.form.valid).toBeTrue();
    });

    it('does nothing without an appointment', () => {
      const c = create();
      c.appointmentId = null;
      fillForm(c);

      c.confirm();

      expect(approval['approveAppointment']).not.toHaveBeenCalled();
    });

    it('does nothing while the form is incomplete', () => {
      const c = create();

      c.confirm();

      expect(approval['approveAppointment']).not.toHaveBeenCalled();
    });

    it('does nothing while an approval is already running', () => {
      const c = create();
      fillForm(c);
      c.isBusy = true;

      c.confirm();

      expect(approval['approveAppointment']).not.toHaveBeenCalled();
    });
  });

  describe('approving', () => {
    it('sends the appointment id and the chosen responsible user', () => {
      const c = create();
      fillForm(c);

      c.confirm();

      const [id, body] = approval['approveAppointment'].calls.mostRecent().args;
      expect(id).toBe('appt-1');
      expect(body.primaryResponsibleUserId).toBe('u-1');
    });

    it('trims the comment', () => {
      const c = create();
      fillForm(c, '  looks right  ');

      c.confirm();

      expect(approval['approveAppointment'].calls.mostRecent().args[1].internalUserComments).toBe(
        'looks right',
      );
    });

    it('sends NULL for a whitespace-only comment rather than a blank string', () => {
      // A stored blank reads downstream as "a comment was left", which it was not.
      const c = create();
      fillForm(c, '   ');

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
      fillForm(c);
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
      fillForm(c);
      approval['approveAppointment'].and.returnValue(throwError(() => ({ status: 409 })));

      c.confirm();

      expect(c.visible).withContext('the modal must not close').toBeTrue();
      expect(c.isBusy).toBeFalse();
      expect(toaster.success).not.toHaveBeenCalled();
    });

    it('keeps the chosen user after a failure so the approval can be retried', () => {
      const c = create();
      fillForm(c, 'a note');
      approval['approveAppointment'].and.returnValue(throwError(() => ({ status: 409 })));

      c.confirm();

      expect(c.form.getRawValue().primaryResponsibleUserId).toBe('u-1');
      expect(c.form.getRawValue().internalUserComments).toBe('a note');
    });

    it('does not tell the parent anything when the approval fails', () => {
      const c = create();
      fillForm(c);
      approval['approveAppointment'].and.returnValue(throwError(() => ({ status: 409 })));
      const emitted: unknown[] = [];
      c.succeeded.subscribe((dto: unknown) => emitted.push(dto));

      c.confirm();

      expect(emitted).toEqual([]);
    });
  });
});
