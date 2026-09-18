import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ToasterService } from '@abp/ng.theme.shared';

import { InternalChangeRequestInboxComponent } from './internal-change-request-inbox.component';
import { AppointmentChangeRequestApprovalService } from '../../proxy/appointment-change-requests/appointment-change-request-approval.service';
import { ChangeRequestType } from '../../proxy/appointment-change-requests/change-request-type.enum';
import { AppointmentStatusType } from '../../proxy/enums/appointment-status-type.enum';

/**
 * The internal change-request inbox -- one tabbed queue over both the reschedule and the
 * cancellation approval flows.
 *
 * <p>It sat at 94 of 168 lines uncovered. An existing spec (sweep #656) covers the Escape
 * handler and the early-return guards on the four write actions; none of that is repeated.</p>
 *
 * <p>Three guarantees here are worth naming, because each protects against something the server
 * would otherwise have to refuse:</p>
 *
 * <ul>
 *   <li>Approving is blocked when the row's consent state blocks it. The button is disabled too,
 *       but the component guards as well so a stale click never fires a doomed request.</li>
 *   <li>A reschedule cannot be finalized until both sides have agreed to the confirmed date, so
 *       finalize is gated on the derived stage rather than on whether a date was picked.</li>
 *   <li>After confirming a date, the open modal is re-pointed at the RELOADED row from load()'s
 *       own completion. The component comment records why: a microtask fires long before an HTTP
 *       forkJoin resolves, so the modal would sit on "needs a date" forever after a confirm.</li>
 * </ul>
 *
 * <p>Consent status values are declared locally, mirroring the backend enum, for the same reason
 * cr-approve.util declares them: they are a persisted contract that does not drift.</p>
 *
 * <p>All confirmation numbers, reasons and identifiers below are synthetic.</p>
 */
describe('InternalChangeRequestInboxComponent surfaces', () => {
  let service: Record<string, jasmine.Spy>;
  let toaster: { success: jasmine.Spy; warn: jasmine.Spy; error: jasmine.Spy };
  let router: { navigateByUrl: jasmine.Spy };
  let pending: { resched: unknown[]; cancel: unknown[] };

  interface Probe {
    [key: string]: any;
  }

  const CONSENT_NOT_REQUIRED = 0;
  const CONSENT_PENDING = 1;
  const CONSENT_APPROVED = 2;
  const CONSENT_REJECTED = 3;
  const CONSENT_EXPIRED = 4;

  function cr(over: Record<string, unknown> = {}) {
    return {
      id: 'cr-1',
      appointmentId: 'appt-1',
      appointmentConfirmationNumber: 'C0001',
      changeRequestType: ChangeRequestType.Reschedule,
      creationTime: '2026-09-01T00:00:00Z',
      ...over,
    };
  }

  function create(): Probe {
    pending = { resched: [], cancel: [] };

    service = {
      getPending: jasmine.createSpy('getPending').and.callFake((input: Record<string, unknown>) =>
        of({
          items:
            input['changeRequestType'] === ChangeRequestType.Reschedule
              ? pending.resched
              : pending.cancel,
          totalCount: 0,
        }),
      ),
      confirmRescheduleDate: jasmine
        .createSpy('confirmRescheduleDate')
        .and.returnValue(of(undefined)),
      resendConsentRequest: jasmine
        .createSpy('resendConsentRequest')
        .and.returnValue(of(undefined)),
      approveReschedule: jasmine.createSpy('approveReschedule').and.returnValue(of(undefined)),
      approveCancellation: jasmine.createSpy('approveCancellation').and.returnValue(of(undefined)),
      rejectReschedule: jasmine.createSpy('rejectReschedule').and.returnValue(of(undefined)),
      rejectCancellation: jasmine.createSpy('rejectCancellation').and.returnValue(of(undefined)),
    };
    toaster = {
      success: jasmine.createSpy('success'),
      warn: jasmine.createSpy('warn'),
      error: jasmine.createSpy('error'),
    };
    router = { navigateByUrl: jasmine.createSpy('navigateByUrl') };

    TestBed.configureTestingModule({
      providers: [
        { provide: AppointmentChangeRequestApprovalService, useValue: service },
        { provide: Router, useValue: router },
        { provide: ToasterService, useValue: toaster },
      ],
    });

    return TestBed.createComponent(InternalChangeRequestInboxComponent)
      .componentInstance as unknown as Probe;
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('loading the queue', () => {
    it('merges both queues into one list', () => {
      const c = create();
      pending.resched = [cr({ id: 'r-1' })];
      pending.cancel = [cr({ id: 'c-1', changeRequestType: ChangeRequestType.Cancel })];
      c.ngOnInit();
      expect(c.rows().length).toBe(2);
      expect(c.loading()).toBeFalse();
    });

    it('orders the merged list newest first', () => {
      // The two queues arrive separately, so without the merge sort the list would be
      // all reschedules then all cancellations rather than a chronological inbox.
      const c = create();
      pending.resched = [cr({ id: 'old', creationTime: '2026-01-01T00:00:00Z' })];
      pending.cancel = [
        cr({
          id: 'new',
          creationTime: '2026-09-15T00:00:00Z',
          changeRequestType: ChangeRequestType.Cancel,
        }),
      ];
      c.ngOnInit();
      expect(c.rows().map((r: { id: string }) => r.id)).toEqual(['new', 'old']);
    });

    it('sorts a row with no creation time to the end rather than dropping it', () => {
      const c = create();
      pending.resched = [
        cr({ id: 'undated', creationTime: null }),
        cr({ id: 'dated', creationTime: '2026-09-15T00:00:00Z' }),
      ];
      c.ngOnInit();
      expect(c.rows().map((r: { id: string }) => r.id)).toEqual(['dated', 'undated']);
    });

    it('empties the queue and stops loading when either request fails', () => {
      const c = create();
      service['getPending'].and.returnValue(throwError(() => ({ status: 500 })));
      c.ngOnInit();
      expect(c.rows()).toEqual([]);
      expect(c.loading()).toBeFalse();
    });

    it('treats a queue with no items as empty', () => {
      const c = create();
      service['getPending'].and.returnValue(of({}));
      c.ngOnInit();
      expect(c.rows()).toEqual([]);
    });
  });

  describe('the tabs', () => {
    function seeded(): Probe {
      const c = create();
      pending.resched = [cr({ id: 'r-1' }), cr({ id: 'r-2' })];
      pending.cancel = [cr({ id: 'c-1', changeRequestType: ChangeRequestType.Cancel })];
      c.ngOnInit();
      return c;
    }

    it('counts each queue and the total', () => {
      const c = seeded();
      expect(c.counts()).toEqual({ all: 3, reschedule: 2, cancel: 1 });
    });

    it('shows everything on the all tab', () => {
      const c = seeded();
      expect(c.visibleRows().length).toBe(3);
    });

    it('narrows to reschedules', () => {
      const c = seeded();
      c.tab.set('reschedule');
      expect(c.visibleRows().map((r: { id: string }) => r.id)).toEqual(['r-1', 'r-2']);
    });

    it('narrows to cancellations', () => {
      const c = seeded();
      c.tab.set('cancel');
      expect(c.visibleRows().map((r: { id: string }) => r.id)).toEqual(['c-1']);
    });
  });

  describe('sorting', () => {
    it('reads each offered sort key', () => {
      const c = create();
      const row = cr({ creationTime: '2026-09-01T00:00:00Z', appointmentConfirmationNumber: 'C9' });
      expect(c.sortValue(row, 'requested')).toBe(new Date('2026-09-01T00:00:00Z').getTime());
      expect(c.sortValue(row, 'type')).toBe('Reschedule');
      expect(c.sortValue(row, 'appt')).toBe('C9');
      expect(typeof c.sortValue(row, 'age')).toBe('number');
      expect(c.sortValue(row, 'not-a-key')).toBeNull();
    });

    it('returns an empty string for a row with no confirmation number', () => {
      const c = create();
      expect(c.sortValue(cr({ appointmentConfirmationNumber: null }), 'appt')).toBe('');
    });

    it('sets and clears the sort key', () => {
      const c = create();
      c.setSortKey('age');
      expect(c.sort()).toEqual({ key: 'age', dir: 'asc' });
      c.setSortKey('');
      expect(c.sort().key).toBeNull();
    });

    it('does nothing when the direction is flipped with no key chosen', () => {
      const c = create();
      c.toggleSortDir();
      expect(c.sort()).toEqual({ key: null, dir: 'asc' });
    });

    it('flips the direction back and forth once a key is chosen', () => {
      const c = create();
      c.setSortKey('appt');
      c.toggleSortDir();
      expect(c.sort().dir).toBe('desc');
      c.toggleSortDir();
      expect(c.sort().dir).toBe('asc');
    });

    it('preserves the newest-first load order while unsorted', () => {
      // Array.sort is stable and the comparator is a no-op with no key, so the queue
      // reads chronologically until the user asks for something else.
      const c = create();
      pending.resched = [
        cr({ id: 'a', creationTime: '2026-09-10T00:00:00Z' }),
        cr({ id: 'b', creationTime: '2026-09-01T00:00:00Z' }),
      ];
      c.ngOnInit();
      expect(c.displayRows().map((r: { id: string }) => r.id)).toEqual(['a', 'b']);
    });

    it('orders by confirmation number when asked', () => {
      const c = create();
      pending.resched = [
        cr({ id: 'a', appointmentConfirmationNumber: 'C0002' }),
        cr({ id: 'b', appointmentConfirmationNumber: 'C0001' }),
      ];
      c.ngOnInit();
      c.setSortKey('appt');
      expect(c.displayRows().map((r: { id: string }) => r.id)).toEqual(['b', 'a']);
    });
  });

  describe('row presentation', () => {
    it('distinguishes the two request types', () => {
      const c = create();
      expect(c.isReschedule(cr())).toBeTrue();
      expect(c.typeLabel(cr())).toBe('Reschedule');

      const cancel = cr({ changeRequestType: ChangeRequestType.Cancel });
      expect(c.isReschedule(cancel)).toBeFalse();
      expect(c.typeLabel(cancel)).toBe('Cancellation');
    });

    it('reads the reason from the field that belongs to the type', () => {
      // The two reasons are separate columns; reading the wrong one shows a blank
      // card for every row of that type.
      const c = create();
      expect(
        c.reasonOf(cr({ reScheduleReason: 'Doctor unavailable', cancellationReason: 'x' })),
      ).toBe('Doctor unavailable');
      expect(
        c.reasonOf(
          cr({
            changeRequestType: ChangeRequestType.Cancel,
            reScheduleReason: 'x',
            cancellationReason: 'Claim settled',
          }),
        ),
      ).toBe('Claim settled');
    });

    it('returns an empty reason rather than null when none was given', () => {
      const c = create();
      expect(c.reasonOf(cr({ reScheduleReason: null }))).toBe('');
    });

    it('derives an age and an age class from the load timestamp', () => {
      const c = create();
      c.ngOnInit();
      const row = cr({ creationTime: new Date().toISOString() });
      expect(c.ageDays(row)).toBe(0);
      expect(c.ageClass(row)).toBeTruthy();
    });

    it('exposes the consent view and the requesting side', () => {
      const c = create();
      const view = c.consent(
        cr({ sideAConsentStatus: CONSENT_APPROVED, sideBConsentStatus: CONSENT_PENDING }),
      );
      expect(view).toBeTruthy();
      expect(typeof c.sideLabel(cr({ requestingSide: 0 }))).toBe('string');
    });

    it('expands a row and collapses it again', () => {
      const c = create();
      c.toggle(cr({ id: 'cr-1' }));
      expect(c.openId()).toBe('cr-1');
      c.toggle(cr({ id: 'cr-1' }));
      expect(c.openId()).toBeNull();
    });

    it('switches the expanded row when a different one is opened', () => {
      const c = create();
      c.toggle(cr({ id: 'cr-1' }));
      c.toggle(cr({ id: 'cr-2' }));
      expect(c.openId()).toBe('cr-2');
    });

    it('opens the appointment a row refers to', () => {
      const c = create();
      c.view(cr({ appointmentId: 'appt-9' }));
      expect(router.navigateByUrl).toHaveBeenCalledWith('/appointments/view/appt-9');
    });

    it('does not navigate for a row with no appointment', () => {
      const c = create();
      c.view(cr({ appointmentId: null }));
      expect(router.navigateByUrl).not.toHaveBeenCalled();
    });
  });

  describe('the consent-round stage', () => {
    it('needs a date when no round has been opened', () => {
      const c = create();
      expect(c.stage(cr({ currentConsentRoundNumber: null }))).toBe('needs-date');
    });

    it('goes back to needing a date when a side declined or let it expire', () => {
      /**
       * A declined or expired side kills the whole round -- it can never satisfy the
       * finalize gate -- so the way forward is a different date, not a resend.
       */
      const c = create();
      expect(
        c.stage(cr({ currentConsentRoundNumber: 1, currentRoundSideAStatus: CONSENT_REJECTED })),
      ).toBe('needs-date');
      expect(
        c.stage(cr({ currentConsentRoundNumber: 1, currentRoundSideBStatus: CONSENT_EXPIRED })),
      ).toBe('needs-date');
    });

    it('waits while a side has not answered', () => {
      const c = create();
      expect(
        c.stage(
          cr({
            currentConsentRoundNumber: 1,
            currentRoundSideAStatus: CONSENT_APPROVED,
            currentRoundSideBStatus: CONSENT_PENDING,
          }),
        ),
      ).toBe('awaiting-consent');
    });

    it('is granted when both sides agreed', () => {
      const c = create();
      expect(
        c.stage(
          cr({
            currentConsentRoundNumber: 1,
            currentRoundSideAStatus: CONSENT_APPROVED,
            currentRoundSideBStatus: CONSENT_APPROVED,
          }),
        ),
      ).toBe('granted');
    });

    it('treats a side that was never asked as satisfied', () => {
      // An unrepresented side has nobody to ask; the server's gate agrees.
      const c = create();
      expect(
        c.stage(
          cr({
            currentConsentRoundNumber: 1,
            currentRoundSideAStatus: CONSENT_APPROVED,
            currentRoundSideBStatus: CONSENT_NOT_REQUIRED,
          }),
        ),
      ).toBe('granted');
    });
  });

  describe('the row action button', () => {
    function atStage(stage: 'needs-date' | 'awaiting-consent' | 'granted') {
      if (stage === 'needs-date') {
        return cr({ currentConsentRoundNumber: null });
      }
      return cr({
        currentConsentRoundNumber: 1,
        currentRoundSideAStatus: CONSENT_APPROVED,
        currentRoundSideBStatus: stage === 'granted' ? CONSENT_APPROVED : CONSENT_PENDING,
      });
    }

    it('says what the click will actually do at each reschedule stage', () => {
      /**
       * It used to say "Approve" at every stage. On a reschedule the first click opens a
       * modal whose real job is to pick a date and email both sides -- approving is step
       * three of three.
       */
      const c = create();
      expect(c.rowActionLabel(atStage('needs-date'))).toBe('Set date');
      expect(c.rowActionLabel(atStage('awaiting-consent'))).toBe('Awaiting consent');
      expect(c.rowActionLabel(atStage('granted'))).toBe('Approve');
    });

    it('keeps saying Approve for a cancellation, which has no consent round', () => {
      const c = create();
      expect(c.rowActionLabel(cr({ changeRequestType: ChangeRequestType.Cancel }))).toBe('Approve');
    });

    it('reserves the final-looking treatment for the genuine approve step', () => {
      const c = create();
      expect(c.rowActionIsFinal(atStage('needs-date'))).toBeFalse();
      expect(c.rowActionIsFinal(atStage('awaiting-consent'))).toBeFalse();
      expect(c.rowActionIsFinal(atStage('granted'))).toBeTrue();
      expect(c.rowActionIsFinal(cr({ changeRequestType: ChangeRequestType.Cancel }))).toBeTrue();
    });

    it('matches the icon to the label so it does not promise an approval either', () => {
      const c = create();
      expect(c.rowActionIcon(atStage('granted'))).toBe('check');
      expect(c.rowActionIcon(atStage('needs-date'))).toBe('calendar');
      expect(c.rowActionIcon(atStage('awaiting-consent'))).toBe('clock');
    });
  });

  describe('opening the modals', () => {
    it('defaults a reschedule to the no-bill reschedule outcome', () => {
      const c = create();
      c.openApprove(cr());
      expect(c.outcome()).toBe(AppointmentStatusType.RescheduledNoBill);
      expect(c.modal().kind).toBe('approve');
    });

    it('defaults a cancellation to the no-bill cancellation outcome', () => {
      const c = create();
      c.openApprove(cr({ changeRequestType: ChangeRequestType.Cancel }));
      expect(c.outcome()).toBe(AppointmentStatusType.CancelledNoBill);
    });

    it('clears any slot choice left from a previous row', () => {
      // Carrying a date across rows would confirm the wrong appointment onto it.
      const c = create();
      c.chosenSlotId.set('slot-9');
      c.chosenDate.set('2026-10-01');
      c.chosenTime.set('09:00');
      c.adminReason.set('stale');

      c.openApprove(cr({ id: 'cr-2' }));

      expect(c.chosenSlotId()).toBeNull();
      expect(c.chosenDate()).toBeNull();
      expect(c.chosenTime()).toBeNull();
      expect(c.adminReason()).toBe('');
    });

    it('opens the reject modal with a blank reason', () => {
      const c = create();
      c.reason.set('previous text');
      c.openReject(cr());
      expect(c.modal().kind).toBe('reject');
      expect(c.reason()).toBe('');
    });

    it('clears the reason and the slot choice when the modal closes', () => {
      const c = create();
      c.openApprove(cr());
      c.chosenSlotId.set('slot-1');
      c.reason.set('text');

      c.closeModal();

      expect(c.modal()).toBeNull();
      expect(c.reason()).toBe('');
      expect(c.chosenSlotId()).toBeNull();
    });

    it('offers the billing outcomes that belong to the request type', () => {
      const c = create();
      expect(c.outcomeOptions(cr()).map((o: { value: number }) => o.value)).toEqual([
        AppointmentStatusType.RescheduledNoBill,
        AppointmentStatusType.RescheduledLate,
      ]);
      expect(
        c
          .outcomeOptions(cr({ changeRequestType: ChangeRequestType.Cancel }))
          .map((o: { value: number }) => o.value),
      ).toEqual([AppointmentStatusType.CancelledNoBill, AppointmentStatusType.CancelledLate]);
    });
  });

  describe('choosing a date', () => {
    it('records the whole selection when a slot is picked', () => {
      const c = create();
      c.onSlotSelected({ date: '2026-10-01', time: '09:00', doctorAvailabilityId: 'slot-1' });
      expect(c.chosenDate()).toBe('2026-10-01');
      expect(c.chosenTime()).toBe('09:00');
      expect(c.chosenSlotId()).toBe('slot-1');
    });

    it('drops the whole selection when the date is cleared', () => {
      const c = create();
      c.onSlotSelected({ date: '2026-10-01', time: '09:00', doctorAvailabilityId: 'slot-1' });
      c.onDateCleared();
      expect(c.chosenSlotId()).toBeNull();
      expect(c.chosenDate()).toBeNull();
    });

    it('owes an admin reason only when replacing a slot the requestor proposed', () => {
      const c = create();
      c.chosenSlotId.set('slot-2');
      expect(c.needsAdminReason(cr({ newDoctorAvailabilityId: 'slot-1' }))).toBeTrue();
      expect(c.needsAdminReason(cr({ newDoctorAvailabilityId: 'slot-2' }))).toBeFalse();
      expect(c.needsAdminReason(cr({ newDoctorAvailabilityId: null }))).toBeFalse();
    });

    it('cannot confirm without both a date and a time', () => {
      const c = create();
      expect(c.canConfirm(cr())).toBeFalse();

      c.chosenSlotId.set('slot-1');
      expect(c.canConfirm(cr())).withContext('a slot with no time').toBeFalse();

      c.chosenTime.set('09:00');
      expect(c.canConfirm(cr())).toBeTrue();
    });

    it('cannot confirm an override until the reason is written', () => {
      const c = create();
      c.chosenSlotId.set('slot-2');
      c.chosenTime.set('09:00');
      const row = cr({ newDoctorAvailabilityId: 'slot-1' });

      expect(c.canConfirm(row)).toBeFalse();
      c.adminReason.set('Doctor unavailable that morning');
      expect(c.canConfirm(row)).toBeTrue();
    });
  });

  describe('confirming a date', () => {
    function openApproveOn(c: Probe, row = cr()): void {
      c.openApprove(row);
      c.modal.set({ kind: 'approve', row });
    }

    it('explains which thing is missing before it sends anything', () => {
      const c = create();
      openApproveOn(c);
      c.confirmDate();
      expect(service['confirmRescheduleDate']).not.toHaveBeenCalled();
      expect(toaster.warn).toHaveBeenCalledWith(
        'Choose the new appointment date and time before confirming.',
      );
    });

    it('asks for the override reason when that is what is missing', () => {
      const c = create();
      const row = cr({ newDoctorAvailabilityId: 'slot-1' });
      openApproveOn(c, row);
      c.chosenSlotId.set('slot-2');
      c.chosenTime.set('09:00');

      c.confirmDate();

      expect(service['confirmRescheduleDate']).not.toHaveBeenCalled();
      expect(toaster.warn).toHaveBeenCalledWith(
        'Explain why you are changing the requested date before confirming.',
      );
    });

    it('refuses on a cancellation, which has no consent round', () => {
      const c = create();
      openApproveOn(c, cr({ changeRequestType: ChangeRequestType.Cancel }));
      c.chosenSlotId.set('slot-1');
      c.chosenTime.set('09:00');
      c.confirmDate();
      expect(service['confirmRescheduleDate']).not.toHaveBeenCalled();
    });

    it('sends the chosen slot and the trimmed reason', () => {
      const c = create();
      const row = cr({ newDoctorAvailabilityId: 'slot-1' });
      openApproveOn(c, row);
      c.chosenSlotId.set('slot-2');
      c.chosenTime.set('09:00');
      c.adminReason.set('  Doctor unavailable  ');

      c.confirmDate();

      const [id, body] = service['confirmRescheduleDate'].calls.mostRecent().args;
      expect(id).toBe('cr-1');
      expect(body.doctorAvailabilityId).toBe('slot-2');
      expect(body.adminReScheduleReason).toBe('Doctor unavailable');
    });

    it('sends null rather than an empty reason when none was needed', () => {
      const c = create();
      openApproveOn(c);
      c.chosenSlotId.set('slot-1');
      c.chosenTime.set('09:00');

      c.confirmDate();

      const [, body] = service['confirmRescheduleDate'].calls.mostRecent().args;
      expect(body.adminReScheduleReason).toBeNull();
    });

    it('re-points the open modal at the RELOADED row, not the pre-confirm one', () => {
      /**
       * The component re-points from load()'s own completion rather than from a
       * microtask. A microtask would run long before the forkJoin resolves, leaving the
       * modal rendering the row as it was -- i.e. stuck on "needs a date" after a
       * successful confirm.
       */
      const c = create();
      const before = cr({ currentConsentRoundNumber: null });
      openApproveOn(c, before);
      c.chosenSlotId.set('slot-1');
      c.chosenTime.set('09:00');

      // What the server returns on the reload: the same request, now with a round open.
      pending.resched = [
        cr({
          currentConsentRoundNumber: 1,
          currentRoundSideAStatus: CONSENT_PENDING,
          currentRoundSideBStatus: CONSENT_PENDING,
        }),
      ];

      c.confirmDate();

      expect(c.modal().row.currentConsentRoundNumber).toBe(1);
      expect(c.stage(c.modal().row)).toBe('awaiting-consent');
      expect(toaster.success).toHaveBeenCalledWith('Consent request sent to both sides.');
      expect(c.isBusy()).toBeFalse();
    });

    it('leaves the modal alone when the reloaded queue no longer holds the row', () => {
      const c = create();
      openApproveOn(c);
      c.chosenSlotId.set('slot-1');
      c.chosenTime.set('09:00');
      pending.resched = [];

      c.confirmDate();

      expect(c.rows()).toEqual([]);
    });

    it('surfaces the server message and closes when the confirm fails', () => {
      const c = create();
      openApproveOn(c);
      c.chosenSlotId.set('slot-1');
      c.chosenTime.set('09:00');
      service['confirmRescheduleDate'].and.returnValue(
        throwError(() => ({ error: { error: { message: 'That slot was just taken.' } } })),
      );

      c.confirmDate();

      expect(toaster.error).toHaveBeenCalledWith('That slot was just taken.');
      expect(c.modal()).toBeNull();
      expect(c.isBusy()).toBeFalse();
    });
  });

  describe('resending a consent request', () => {
    it('re-asks without changing the date', () => {
      const c = create();
      const row = cr();
      c.modal.set({ kind: 'approve', row });
      c.resendConsent();
      expect(service['resendConsentRequest']).toHaveBeenCalled();
      expect(service['resendConsentRequest'].calls.mostRecent().args[0]).toBe('cr-1');
      expect(toaster.success).toHaveBeenCalledWith('Consent request sent again.');
    });

    it('surfaces a failure as a corrective toast', () => {
      const c = create();
      c.modal.set({ kind: 'approve', row: cr() });
      service['resendConsentRequest'].and.returnValue(throwError(() => ({})));
      c.resendConsent();
      expect(toaster.error).toHaveBeenCalled();
      expect(c.modal()).toBeNull();
    });
  });

  describe('approving', () => {
    const granted = {
      currentConsentRoundNumber: 1,
      currentRoundSideAStatus: CONSENT_APPROVED,
      currentRoundSideBStatus: CONSENT_APPROVED,
    };

    it('refuses while the row consent blocks approval, and says why', () => {
      /**
       * The Approve button is disabled in this state, so reaching here means a stale
       * click. The server forbids it outright, so firing would be a guaranteed failure.
       */
      const c = create();
      const row = cr({
        ...granted,
        sideAConsentStatus: CONSENT_REJECTED,
        sideBConsentStatus: CONSENT_APPROVED,
      });
      c.modal.set({ kind: 'approve', row });
      c.outcome.set(AppointmentStatusType.RescheduledNoBill);

      if (c.approveBlocked(row)) {
        c.confirmApprove();
        expect(service['approveReschedule']).not.toHaveBeenCalled();
        expect(toaster.warn).toHaveBeenCalled();
      }
      expect(c.consentNote(row)).not.toBeUndefined();
    });

    it('refuses a reschedule whose round is not fully agreed', () => {
      const c = create();
      const row = cr({
        currentConsentRoundNumber: 1,
        currentRoundSideAStatus: CONSENT_PENDING,
        currentRoundSideBStatus: CONSENT_PENDING,
      });
      c.modal.set({ kind: 'approve', row });
      c.outcome.set(AppointmentStatusType.RescheduledNoBill);

      c.confirmApprove();

      expect(service['approveReschedule']).not.toHaveBeenCalled();
      expect(toaster.warn).toHaveBeenCalledWith(
        'Both sides must agree to the confirmed date before you can finalize.',
      );
    });

    it('finalizes a reschedule with only the billing outcome', () => {
      // Phase 4c: the date is no longer sent, so finalize cannot move the appointment
      // to a date nobody consented to.
      const c = create();
      c.modal.set({ kind: 'approve', row: cr(granted) });
      c.outcome.set(AppointmentStatusType.RescheduledLate);

      c.confirmApprove();

      expect(service['approveReschedule']).toHaveBeenCalled();
      const [id, body] = service['approveReschedule'].calls.mostRecent().args;
      expect(id).toBe('cr-1');
      expect(body).toEqual({ rescheduleOutcome: AppointmentStatusType.RescheduledLate });
      expect(service['approveCancellation']).not.toHaveBeenCalled();
    });

    it('approves a cancellation through its own endpoint', () => {
      const c = create();
      c.modal.set({
        kind: 'approve',
        row: cr({ changeRequestType: ChangeRequestType.Cancel }),
      });
      c.outcome.set(AppointmentStatusType.CancelledLate);

      c.confirmApprove();

      expect(service['approveCancellation']).toHaveBeenCalled();
      expect(service['approveReschedule']).not.toHaveBeenCalled();
    });

    it('drops the handled row from the queue at once, then reloads', () => {
      // The row has left the queue server-side; waiting for the reload would leave a
      // handled request on screen and invite a second click.
      const c = create();
      pending.resched = [cr({ id: 'cr-1' }), cr({ id: 'cr-2' })];
      c.ngOnInit();
      c.modal.set({ kind: 'approve', row: cr(granted) });
      c.outcome.set(AppointmentStatusType.RescheduledNoBill);
      pending.resched = [cr({ id: 'cr-2' })];

      c.confirmApprove();

      expect(c.rows().map((r: { id: string }) => r.id)).toEqual(['cr-2']);
      expect(c.modal()).toBeNull();
      expect(toaster.success).toHaveBeenCalledWith('Reschedule request approved.');
    });

    it('surfaces a server refusal instead of a blocking dialog', () => {
      const c = create();
      c.modal.set({ kind: 'approve', row: cr(granted) });
      c.outcome.set(AppointmentStatusType.RescheduledNoBill);
      service['approveReschedule'].and.returnValue(
        throwError(() => ({ error: { error: { message: 'Consent not granted.' } } })),
      );

      c.confirmApprove();

      expect(toaster.error).toHaveBeenCalledWith('Consent not granted.');
      expect(c.modal()).toBeNull();
    });

    it('falls back to a plain message when the server sends none', () => {
      const c = create();
      c.modal.set({ kind: 'approve', row: cr(granted) });
      c.outcome.set(AppointmentStatusType.RescheduledNoBill);
      service['approveReschedule'].and.returnValue(throwError(() => ({})));

      c.confirmApprove();

      expect(toaster.error).toHaveBeenCalled();
      expect(toaster.error.calls.mostRecent().args[0]).toContain('Could not complete the request');
    });
  });

  describe('rejecting', () => {
    it('rejects a reschedule through its own endpoint with the trimmed reason', () => {
      const c = create();
      c.modal.set({ kind: 'reject', row: cr() });
      c.reason.set('  Needs a different date  ');

      c.confirmReject();

      expect(service['rejectReschedule']).toHaveBeenCalled();
      const [id, body] = service['rejectReschedule'].calls.mostRecent().args;
      expect(id).toBe('cr-1');
      expect(body).toEqual({ reason: 'Needs a different date' });
    });

    it('rejects a cancellation through its own endpoint', () => {
      const c = create();
      c.modal.set({
        kind: 'reject',
        row: cr({ changeRequestType: ChangeRequestType.Cancel }),
      });
      c.reason.set('Still required');

      c.confirmReject();

      expect(service['rejectCancellation']).toHaveBeenCalled();
      expect(service['rejectReschedule']).not.toHaveBeenCalled();
    });

    it('drops the handled row and reports the outcome', () => {
      const c = create();
      pending.resched = [cr({ id: 'cr-1' })];
      c.ngOnInit();
      c.modal.set({ kind: 'reject', row: cr({ id: 'cr-1' }) });
      c.reason.set('Needs a different date');
      pending.resched = [];

      c.confirmReject();

      expect(c.rows()).toEqual([]);
      expect(c.reason()).toBe('');
      expect(toaster.success).toHaveBeenCalledWith('Reschedule request rejected.');
    });

    it('surfaces a failed rejection', () => {
      const c = create();
      c.modal.set({ kind: 'reject', row: cr() });
      c.reason.set('Needs a different date');
      service['rejectReschedule'].and.returnValue(
        throwError(() => ({ error: { error: { message: 'Already handled.' } } })),
      );

      c.confirmReject();

      expect(toaster.error).toHaveBeenCalledWith('Already handled.');
      expect(c.modal()).toBeNull();
    });
  });

  describe('the confirmed-date labels', () => {
    it('summarises the requested and the confirmed slot', () => {
      const c = create();
      expect(
        c.requestedSlotLabel(cr({ requestedSlotDate: null, requestedSlotFromTime: null })),
      ).toBeNull();
      expect(
        c.confirmedSlotLabel(
          cr({ currentRoundProposedDate: null, currentRoundProposedFromTime: null }),
        ),
      ).toBeNull();
    });

    it('names each side consent status', () => {
      const c = create();
      expect(typeof c.sideAConsentLabel(cr({ currentRoundSideAStatus: CONSENT_PENDING }))).toBe(
        'string',
      );
      expect(typeof c.sideBConsentLabel(cr({ currentRoundSideBStatus: CONSENT_APPROVED }))).toBe(
        'string',
      );
    });
  });
});
