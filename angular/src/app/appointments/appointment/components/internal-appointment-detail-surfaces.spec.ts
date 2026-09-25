import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { FormBuilder } from '@angular/forms';
import { of } from 'rxjs';
import {
  ConfigStateService,
  EnvironmentService,
  LocalizationService,
  PermissionService,
  RestService,
} from '@abp/ng.core';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';

import { InternalAppointmentDetailComponent } from './internal-appointment-detail.component';
import { AppointmentViewComponent } from './appointment-view.component';
import { AppointmentService } from '../../../proxy/appointments/appointment.service';
import { AppointmentChangeRequestService } from '../../../proxy/appointment-change-requests/appointment-change-request.service';
import { AppointmentInfoRequestService } from '../../../proxy/appointment-info-requests/appointment-info-request.service';
import { AppointmentStatusType } from '../../../proxy/enums/appointment-status-type.enum';
import { ChangeRequestType } from '../../../proxy/appointment-change-requests/change-request-type.enum';

/**
 * The internal appointment detail -- the staff-side redesign that EXTENDS AppointmentViewComponent.
 *
 * <p>It sat at 72 of 88 lines uncovered. Covering the base class in #937 did NOT move this file:
 * a base class and its subclass are separate files in the coverage report, so the 72 here are
 * entirely this subclass's own surface. That was measured, not assumed.</p>
 *
 * <p>An existing spec (#622) covers the Escape handler; it is not repeated.</p>
 *
 * <p>`super.ngOnInit()` is deliberately NOT driven. It pulls the whole inherited load chain,
 * including the base `loadStateNames`, which subscribes with no error handler -- a defect already
 * logged from tranche 3, and the cause of an unattributable "error thrown in afterAll" that cost
 * a full isolation run. The appointment and history are seeded directly instead, which is where
 * this subclass's own lines live.</p>
 *
 * <p>Inherited members that drive the ones under test (`currentStatus`, `isInternalUser`) are
 * base-class getters, so they are shadowed per test with `Object.defineProperty` rather than
 * reverse-engineered from the base's state.</p>
 *
 * <p>All names, confirmation numbers and identifiers below are synthetic.</p>
 */
describe('InternalAppointmentDetailComponent surfaces', () => {
  let appointments: Record<string, jasmine.Spy>;
  let infoRequests: { getHistory: jasmine.Spy };
  let toaster: { success: jasmine.Spy; error: jasmine.Spy };
  let router: { navigate: jasmine.Spy; navigateByUrl: jasmine.Spy };
  let granted: Set<string>;
  let routeId: string | null;

  interface Probe {
    [key: string]: any;
  }

  function appt(over: Record<string, unknown> = {}) {
    return {
      appointment: {
        id: 'appt-1',
        requestConfirmationNumber: 'C0001',
        appointmentDate: '2026-10-01T09:00:00',
        creationTime: '2026-09-01T09:00:00',
        lastModificationTime: '2026-09-02T09:00:00',
        ...((over['appointment'] as Record<string, unknown>) ?? {}),
      },
      appointmentType: { name: 'AME' },
      location: { name: 'Encino' },
      ...over,
    };
  }

  function create(options: { policies?: string[] } = {}): Probe {
    granted = new Set(options.policies ?? []);
    routeId = 'appt-1';

    appointments = {
      getWithNavigationProperties: jasmine
        .createSpy('getWithNavigationProperties')
        .and.returnValue(of(appt())),
    };
    infoRequests = { getHistory: jasmine.createSpy('getHistory').and.returnValue(of([])) };
    toaster = { success: jasmine.createSpy('success'), error: jasmine.createSpy('error') };
    router = {
      navigate: jasmine.createSpy('navigate'),
      navigateByUrl: jasmine.createSpy('navigateByUrl'),
    };

    TestBed.configureTestingModule({
      providers: [
        FormBuilder,
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => routeId } } },
        },
        { provide: Router, useValue: router },
        { provide: HttpClient, useValue: { get: () => of(null), post: () => of(null) } },
        { provide: AppointmentService, useValue: appointments },
        { provide: AppointmentChangeRequestService, useValue: {} },
        { provide: AppointmentInfoRequestService, useValue: infoRequests },
        { provide: ConfigStateService, useValue: { getOne: () => null, getAll: () => ({}) } },
        { provide: ConfirmationService, useValue: { warn: () => of(null) } },
        { provide: EnvironmentService, useValue: { getApiUrl: () => '' } },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
        {
          provide: PermissionService,
          useValue: { getGrantedPolicy: (p: string) => granted.has(p) },
        },
        { provide: RestService, useValue: { request: () => of(null) } },
        { provide: ToasterService, useValue: toaster },
      ],
    });

    return TestBed.createComponent(InternalAppointmentDetailComponent)
      .componentInstance as unknown as Probe;
  }

  /** Shadow an inherited getter for the duration of one test. */
  function shadow(c: Probe, name: string, value: unknown): void {
    Object.defineProperty(c, name, { value, configurable: true });
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('the status banner', () => {
    it('derives the pill from the inherited status', () => {
      const c = create();
      shadow(c, 'currentStatus', AppointmentStatusType.Approved);
      expect(c.pill).toBe('Approved');
    });

    it('treats an absent status as pending', () => {
      const c = create();
      shadow(c, 'currentStatus', null);
      expect(c.pill).toBe('Pending');
    });

    it('derives the variant and the label from the pill', () => {
      const c = create();
      shadow(c, 'currentStatus', AppointmentStatusType.Approved);
      expect(c.bannerVariant).toBeTruthy();
      expect(c.statusLabel).toBeTruthy();
    });
  });

  describe('the action set', () => {
    it('offers nothing at all to a non-internal viewer', () => {
      // The external detail is a separate route; if this one ever rendered for an
      // external user, it must not offer staff actions.
      const c = create();
      shadow(c, 'isInternalUser', false);
      shadow(c, 'currentStatus', AppointmentStatusType.Pending);
      expect(c.actions).toEqual([]);
      expect(c.can('approve')).toBeFalse();
    });

    it('offers the status-appropriate actions to internal staff', () => {
      const c = create();
      shadow(c, 'isInternalUser', true);
      shadow(c, 'currentStatus', AppointmentStatusType.Pending);
      expect(c.actions.length).toBeGreaterThan(0);
    });

    it('answers can() from the action set', () => {
      const c = create();
      shadow(c, 'isInternalUser', true);
      shadow(c, 'currentStatus', AppointmentStatusType.Pending);
      const first = c.actions[0];
      expect(c.can(first)).toBeTrue();
      expect(c.can('not-an-action')).toBeFalse();
    });
  });

  describe('the send-back review cards', () => {
    const resolvedRound = {
      isResolved: true,
      roundNumber: 1,
      note: 'Please confirm the claim number.',
    };

    it('badges a resubmission only while the request is still pending', () => {
      const c = create();
      c.infoHistory = [{ ...resolvedRound, resubmittedAt: '2026-09-03T00:00:00Z' }];

      shadow(c, 'currentStatus', AppointmentStatusType.Pending);
      const pendingBadge = c.showResubmittedBadge;

      shadow(c, 'currentStatus', AppointmentStatusType.Approved);
      expect(c.showResubmittedBadge)
        .withContext('an approved request no longer needs the badge')
        .toBeFalse();
      expect(typeof pendingBadge).toBe('boolean');
    });

    it('shows no badge with no history at all', () => {
      const c = create();
      shadow(c, 'currentStatus', AppointmentStatusType.Pending);
      c.infoHistory = [];
      expect(c.showResubmittedBadge).toBeFalse();
    });

    it('surfaces the latest round only once it is resolved', () => {
      const c = create();
      c.infoHistory = [{ isResolved: false, roundNumber: 1 }];
      expect(c.resubmittedRound).toBeNull();

      c.infoHistory = [resolvedRound];
      expect(c.resubmittedRound).not.toBeNull();
    });

    it('has no diff rows without a resolved round', () => {
      const c = create();
      c.infoHistory = [];
      expect(c.diffRows).toEqual([]);
    });

    it('opens the diff card and collapses the history card by default', () => {
      // The diff is what changed since the request; the full history is reference.
      const c = create();
      expect(c.diffOpen).toBeTrue();
      expect(c.historyOpen).toBeFalse();
    });

    it('toggles each card independently', () => {
      const c = create();
      c.toggleDiff();
      c.toggleHistory();
      expect(c.diffOpen).toBeFalse();
      expect(c.historyOpen).toBeTrue();

      c.toggleDiff();
      c.toggleHistory();
      expect(c.diffOpen).toBeTrue();
      expect(c.historyOpen).toBeFalse();
    });

    it('summarises a round through the shared helpers', () => {
      const c = create();
      expect(typeof c.roundFixedSummary(resolvedRound)).toBe('string');
      expect(typeof c.roundFlaggedSummary(resolvedRound)).toBe('string');
      expect(typeof c.roundNotePreview('a note')).toBe('string');
      expect(typeof c.roundNotePreview(null)).toBe('string');
    });
  });

  describe('the meta accessors', () => {
    it('reads the type, location, confirmation and dates off the DTO', () => {
      const c = create();
      c.appointment = appt();
      expect(c.apptTypeName).toBe('AME');
      expect(c.locationName).toBe('Encino');
      expect(c.confNo).toBe('C0001');
      expect(c.apptDate).toBe('2026-10-01T09:00:00');
      expect(c.requestedOn).toBe('2026-09-01T09:00:00');
      expect(c.modifiedOn).toBe('2026-09-02T09:00:00');
    });

    it('returns empty strings rather than undefined with nothing loaded', () => {
      const c = create();
      c.appointment = null;
      expect(c.apptTypeName).toBe('');
      expect(c.locationName).toBe('');
      expect(c.confNo).toBe('');
    });

    it('reads the joint-declaration marker from the DTO, NOT through the form', () => {
      /**
       * The marker is an internal stamp with no form control behind it, so the original
       * `fv()` read returned '' every time and the notice below it was unreachable. Found
       * by running against the local stack: the list badge rendered and the detail stayed
       * blank. This is what keeps the two reading the same field.
       */
      const c = create();
      c.appointment = appt({ appointment: { jointDeclarationOverdueAt: '2026-09-10T00:00:00Z' } });
      expect(c.isJointDeclarationOverdue).toBeTrue();

      c.appointment = appt();
      expect(c.isJointDeclarationOverdue).toBeFalse();
    });
  });

  describe('the read ledger', () => {
    it('renders a form value as a display string', () => {
      const c = create();
      c.form.patchValue({ patientFirstName: 'Ada' });
      expect(c.fv('patientFirstName')).toBe('Ada');
      expect(c.fv('not-a-control')).toBe('');
    });

    it('treats null, undefined and empty alike as nothing to show', () => {
      const c = create();
      c.form.patchValue({ patientFirstName: null });
      expect(c.fv('patientFirstName')).toBe('');
    });

    it('joins the patient name from its two parts', () => {
      const c = create();
      c.form.patchValue({ patientFirstName: 'Ada', patientLastName: 'Lovelace' });
      expect(c.patientDisplayName).toBe('Ada Lovelace');
    });

    it('shows a single name part without a stray space', () => {
      const c = create();
      c.form.patchValue({ patientFirstName: 'Ada', patientLastName: '' });
      expect(c.patientDisplayName).toBe('Ada');
    });

    it('names the gender from the enum options', () => {
      const c = create();
      c.form.patchValue({ patientGenderId: 2 });
      expect(c.genderLabel).toBeTruthy();

      c.form.patchValue({ patientGenderId: 9999 });
      expect(c.genderLabel).toBe('');
    });

    it('reports whether an interpreter is needed', () => {
      const c = create();
      c.form.patchValue({ patientNeedsInterpreter: true });
      expect(c.needsInterpreter).toBeTrue();

      c.form.patchValue({ patientNeedsInterpreter: null });
      expect(c.needsInterpreter).toBeFalse();
    });

    it('resolves a language id only once the lookup has been loaded', () => {
      // The map is filled by ngOnInit, which this spec does not drive; an unknown id
      // must read as blank rather than as the raw GUID.
      const c = create();
      expect(c.languageName('lang-1')).toBe('');
      expect(c.languageName(null)).toBe('');
      expect(c.languageName(undefined)).toBe('');
    });
  });

  describe('the staff panel', () => {
    it('resolves the booker email through the shared helper', () => {
      const c = create();
      c.appointment = appt();
      expect(typeof c.bookerEmail).toBe('string');
    });

    it('offers a decide-by deadline only while pending', () => {
      const c = create();
      c.appointment = appt();

      shadow(c, 'currentStatus', AppointmentStatusType.Approved);
      expect(c.decideBy).toBeNull();

      shadow(c, 'currentStatus', AppointmentStatusType.Pending);
      expect(c.decideBy).not.toBeNull();
    });

    it('shows approval comments only on an approved request', () => {
      const c = create();
      c.form.patchValue({ internalUserComments: 'Looks fine.' });

      shadow(c, 'currentStatus', AppointmentStatusType.Pending);
      expect(c.approvalComments).toBe('');

      shadow(c, 'currentStatus', AppointmentStatusType.Approved);
      expect(c.approvalComments).toBe('Looks fine.');
    });

    it('shows a rejection reason only on a rejected request', () => {
      // Showing either on the wrong status would attribute a note to the wrong decision.
      const c = create();
      c.form.patchValue({ rejectionNotes: 'Wrong claim number.' });

      shadow(c, 'currentStatus', AppointmentStatusType.Approved);
      expect(c.rejectionReason).toBe('');

      shadow(c, 'currentStatus', AppointmentStatusType.Rejected);
      expect(c.rejectionReason).toBe('Wrong claim number.');
    });
  });

  describe('edit-details mode', () => {
    it('snapshots the form on entry so cancel can revert it', () => {
      const c = create();
      c.form.patchValue({ patientFirstName: 'Ada' });

      c.enterEdit();
      expect(c.editMode).toBeTrue();

      c.form.patchValue({ patientFirstName: 'Edited' });
      c.cancelEdit();

      expect(c.form.get('patientFirstName').value).toBe('Ada');
      expect(c.editMode).toBeFalse();
    });

    it('leaves edit mode even with nothing snapshotted', () => {
      const c = create();
      c.editMode = true;
      expect(() => c.cancelEdit()).not.toThrow();
      expect(c.editMode).toBeFalse();
    });

    it('leaves edit mode once the inherited save succeeds', async () => {
      const c = create();
      c.save = jasmine.createSpy('save').and.returnValue(Promise.resolve());
      c.enterEdit();

      await c.saveEdit();

      expect(c.save).toHaveBeenCalled();
      expect(c.editMode).toBeFalse();
    });

    it('STAYS in edit mode when the save fails, so the work is not lost', async () => {
      /**
       * The parent sets errorMessage and rethrows. Dropping out of edit mode here would
       * hide the form the user still needs, with their changes on screen but uneditable.
       */
      const c = create();
      c.save = jasmine.createSpy('save').and.returnValue(Promise.reject(new Error('409')));
      c.enterEdit();

      await c.saveEdit();

      expect(c.editMode).toBeTrue();
    });
  });

  describe('the action launchers', () => {
    it('delegates approve and reject to the inherited engine', () => {
      const c = create();
      c.dispatchAction = jasmine.createSpy('dispatchAction');

      c.approve();
      c.reject();

      expect(c.dispatchAction.calls.allArgs()).toEqual([['approve'], ['reject']]);
    });

    it('delegates reschedule, cancel and request-info to their inherited openers', () => {
      const c = create();
      c.openRescheduleRequest = jasmine.createSpy('openRescheduleRequest');
      c.openCancelRequest = jasmine.createSpy('openCancelRequest');
      c.openRequestInfo = jasmine.createSpy('openRequestInfo');

      c.reschedule();
      c.cancel();
      c.requestInfo();

      expect(c.openRescheduleRequest).toHaveBeenCalled();
      expect(c.openCancelRequest).toHaveBeenCalled();
      expect(c.openRequestInfo).toHaveBeenCalled();
    });

    it('goes back to the appointments list', () => {
      const c = create();
      c.back();
      expect(router.navigateByUrl).toHaveBeenCalledWith('/appointments');
    });

    it('opens the change log for the loaded appointment', () => {
      const c = create();
      c.appointment = appt();
      c.openChangeLog();
      expect(router.navigate).toHaveBeenCalledWith(['/appointments/view', 'appt-1', 'change-log']);
    });

    it('does not open a change log with nothing loaded', () => {
      const c = create();
      c.appointment = null;
      c.openChangeLog();
      expect(router.navigate).not.toHaveBeenCalled();
    });

    it('gates the change log and the demographics download on their own permissions', () => {
      const c = create({ policies: ['CaseEvaluation.AppointmentChangeLogs'] });
      expect(c.canViewChangeLog).toBeTrue();
      expect(c.canDownloadDemographics).toBeFalse();
    });
  });

  describe('when a change request is submitted', () => {
    it('confirms a reschedule and a cancellation with different wording', () => {
      // B3: a staff-initiated request stays Pending for a supervisor to finalize after
      // both parties consent -- it never auto-approves, so the toast must not say approved.
      const c = create();
      c.appointment = appt();

      c.onChangeRequestSucceeded({ changeRequestType: ChangeRequestType.Reschedule });
      expect(toaster.success).toHaveBeenCalledWith('::Appointment:Toast:RescheduleRequested');

      c.onChangeRequestSucceeded({ changeRequestType: ChangeRequestType.Cancel });
      expect(toaster.success).toHaveBeenCalledWith('::Appointment:Toast:CancelRequested');
    });

    it('reloads both the history and the appointment', () => {
      const c = create();
      c.appointment = appt();

      c.onChangeRequestSucceeded({ changeRequestType: ChangeRequestType.Cancel });

      expect(infoRequests.getHistory).toHaveBeenCalledWith('appt-1');
      expect(appointments['getWithNavigationProperties']).toHaveBeenCalledWith('appt-1');
    });

    it('falls back to the route id when nothing is loaded yet', () => {
      const c = create();
      c.appointment = null;
      routeId = 'from-route';

      c.onChangeRequestSucceeded({ changeRequestType: ChangeRequestType.Cancel });

      expect(appointments['getWithNavigationProperties']).toHaveBeenCalledWith('from-route');
    });

    it('does nothing when there is no id from either source', () => {
      const c = create();
      c.appointment = null;
      routeId = null;

      c.onChangeRequestSucceeded({ changeRequestType: ChangeRequestType.Cancel });

      expect(appointments['getWithNavigationProperties']).not.toHaveBeenCalled();
    });

    it('stores the reloaded appointment', () => {
      const c = create();
      c.appointment = appt();
      appointments['getWithNavigationProperties'].and.returnValue(
        of(appt({ appointment: { id: 'appt-1', requestConfirmationNumber: 'C0002' } })),
      );

      c.onChangeRequestSucceeded({ changeRequestType: ChangeRequestType.Cancel });

      expect(c.confNo).toBe('C0002');
    });

    /**
     * A FAILING history reload is deliberately NOT tested here, and the reason is a defect
     * rather than a limitation.
     *
     * `loadHistory` (internal-appointment-detail.component.ts:154) subscribes with a `next`
     * handler and NO `error` handler, so a failing getHistory produces an unhandled RxJS
     * error. In the browser that surfaces as "An error was thrown in afterAll / [object
     * Object] thrown" AFTER every test has passed -- no failing test attached -- and it
     * took the whole run down when these seven specs ran together.
     *
     * The test that was here asserted `not.toThrow()`, which PASSED: the error is
     * asynchronous and unhandled, so nothing is thrown synchronously for the assertion to
     * catch. It proved nothing while reading as though it proved the component copes.
     *
     * Same shape as the base class's `loadStateNames`, already on the backlog; this one is
     * logged alongside it.
     */
    it('treats a null history payload as no rounds', () => {
      const c = create();
      c.appointment = appt();
      infoRequests.getHistory.and.returnValue(of(null));
      c.onChangeRequestSucceeded({ changeRequestType: ChangeRequestType.Cancel });
      expect(c.infoHistory).toEqual([]);
    });
  });

  /**
   * The subclass's own `ngOnInit`: the appointment-language lookup behind the read ledger, and
   * the send-back history.
   *
   * <p>`super.ngOnInit()` is still not driven, for the reason given at the top of this file. The
   * BASE prototype's `ngOnInit` is replaced with a spy for these tests only (jasmine restores it
   * after each one), which keeps the inherited load chain out while still proving the subclass
   * calls it.</p>
   */
  describe('initialisation', () => {
    let baseInit: jasmine.Spy;

    beforeEach(() => {
      baseInit = spyOn(AppointmentViewComponent.prototype, 'ngOnInit');
    });

    /** The inherited lookup is an instance arrow field, so it is replaced per instance. */
    function withLanguages(c: Probe, response: unknown): jasmine.Spy {
      const lookup = jasmine
        .createSpy('getAppointmentLanguageLookup')
        .and.returnValue(of(response));
      c.getAppointmentLanguageLookup = lookup;
      return lookup;
    }

    it('still runs the inherited load', () => {
      const c = create();
      withLanguages(c, { items: [] });
      c.ngOnInit();
      expect(baseInit).toHaveBeenCalled();
    });

    it('names the patient language from the lookup it loads', () => {
      const c = create();
      const lookup = withLanguages(c, {
        items: [
          { id: 'lang-1', displayName: 'Spanish' },
          { id: 'lang-2' },
          { displayName: 'No id' },
        ],
      });

      c.ngOnInit();

      expect(lookup).toHaveBeenCalledWith({ filter: '', skipCount: 0, maxResultCount: 100 });
      expect(c.languageName('lang-1')).toBe('Spanish');
      expect(c.languageName('lang-2')).withContext('a row with no display name').toBe('');
      expect(c.languageName('lang-9')).withContext('an id the lookup did not return').toBe('');
      expect(c.languageNamesById.size).withContext('the row with no id is skipped').toBe(2);
    });

    it('treats a lookup with no rows as no languages', () => {
      const c = create();
      withLanguages(c, {});
      c.ngOnInit();
      expect(c.languageNamesById.size).toBe(0);
    });

    it('loads the send-back history for the routed appointment', () => {
      const c = create();
      withLanguages(c, { items: [] });
      c.ngOnInit();
      expect(infoRequests.getHistory).toHaveBeenCalledWith('appt-1');
    });

    it('skips the history when the route carries no id', () => {
      const c = create();
      withLanguages(c, { items: [] });
      routeId = null;

      c.ngOnInit();

      expect(infoRequests.getHistory).not.toHaveBeenCalled();
    });
  });
});
