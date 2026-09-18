import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient, HttpHeaders, HttpResponse } from '@angular/common/http';
import { FormBuilder } from '@angular/forms';
import { Observable, of, throwError } from 'rxjs';
import {
  ConfigStateService,
  EnvironmentService,
  LocalizationService,
  RestService,
} from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';

import { AppointmentViewComponent } from './appointment-view.component';
import { AppointmentService } from '../../../proxy/appointments/appointment.service';
import { AppointmentChangeRequestService } from '../../../proxy/appointment-change-requests/appointment-change-request.service';
import { AppointmentStatusType } from '../../../proxy/enums/appointment-status-type.enum';
import { ChangeRequestType } from '../../../proxy/appointment-change-requests/change-request-type.enum';
import { RequestStatusType } from '../../../proxy/enums/request-status-type.enum';

/**
 * The appointment detail ENGINE -- the base class both detail pages extend.
 *
 * <p>`appointment-view.snapshot.spec.ts` covers the two attorney-snapshot overlays and nothing
 * else, which left 410 of this file's lines uncovered: the whole load path, the role gates that
 * decide who may edit, the save chain and its three distinct failure points, the authorized-user
 * modal, the party lookups, and the change-request surface.</p>
 *
 * <p>Two subclasses inherit all of it -- `InternalAppointmentDetailComponent` for staff and
 * `ExternalAppointmentDetailComponent` for patients and attorneys -- so a defect here reaches
 * both audiences at once.</p>
 *
 * <p>Constructed directly in an injection context, as its sibling spec does; nothing is
 * change-detected, so only the methods each test calls run.</p>
 *
 * <p>Every patient, attorney and employer below is synthetic, and the SSN cases use an obviously
 * non-numeric placeholder token rather than a real-looking number.</p>
 */
describe('AppointmentViewComponent engine', () => {
  let router: { navigate: jasmine.Spy; navigateByUrl: jasmine.Spy };
  let toaster: { success: jasmine.Spy; error: jasmine.Spy };
  let confirmationWarn: jasmine.Spy;
  let restRequest: jasmine.Spy;
  let restResponses: Record<string, Observable<unknown>>;
  let getWithNav: jasmine.Spy;
  let updateAppointment: jasmine.Spy;
  let getCustomFieldValues: jasmine.Spy;
  let getActiveForAppointment: jasmine.Spy;
  let httpGet: jasmine.Spy;
  let routeId: string | null;
  let currentUser: unknown;

  /** Obviously-not-real stand-in for a stored SSN; only its PRESENCE matters. */
  const SSN_PLACEHOLDER = 'SYNTHETIC-PLACEHOLDER';

  interface Probe {
    [key: string]: any;
  }

  function defaultRest(url: string, method: string): Observable<unknown> {
    if (url.includes('state-lookup')) {
      return of({ items: [{ id: 'state-1', displayName: 'California' }] });
    }
    if (url.includes('external-user-lookup')) return of({ items: [] });
    if (url.includes('appointment-accessors')) {
      return method === 'GET' ? of({ items: [] }) : of({});
    }
    if (url.includes('appointment-employer-details')) {
      return method === 'GET' ? of({ items: [] }) : of({});
    }
    if (url.includes('by-appointment')) return of([]);
    if (url.includes('applicant-attorney') || url.includes('defense-attorney')) return of(null);
    if (url.includes('/patients/')) return of({});
    return of({ items: [] });
  }

  function create(
    options: {
      roles?: string[];
      id?: string | null;
      rest?: Record<string, Observable<unknown>>;
      confirmStatus?: unknown;
    } = {},
  ): Probe {
    restResponses = options.rest ?? {};
    routeId = options.id === undefined ? 'appt-1' : options.id;
    currentUser = { id: 'user-1', roles: options.roles ?? [] };

    router = {
      navigate: jasmine.createSpy('navigate'),
      navigateByUrl: jasmine.createSpy('navigateByUrl'),
    };
    toaster = { success: jasmine.createSpy('success'), error: jasmine.createSpy('error') };
    confirmationWarn = jasmine
      .createSpy('warn')
      .and.returnValue(of(options.confirmStatus ?? Confirmation.Status.confirm));
    // Defaults to a LOADED appointment, not null: ngOnInit reads `data.appointment?.panelNumber`,
    // so a null `data` throws before the optional chain helps. Tests that want a specific
    // payload override this; tests that only care about a side effect get a working load.
    getWithNav = jasmine
      .createSpy('getWithNavigationProperties')
      .and.returnValue(of(loadedAppointment()));
    updateAppointment = jasmine.createSpy('update').and.returnValue(of({ id: 'appt-1' }));
    getCustomFieldValues = jasmine
      .createSpy('getAppointmentCustomFieldValues')
      .and.returnValue(of([]));
    getActiveForAppointment = jasmine
      .createSpy('getActiveForAppointment')
      .and.returnValue(of(null));
    httpGet = jasmine.createSpy('get').and.returnValue(of(null));
    restRequest = jasmine
      .createSpy('request')
      .and.callFake((req: { url: string; method: string }) => {
        for (const [fragment, response] of Object.entries(restResponses)) {
          if (req.url.includes(fragment)) return response;
        }
        return defaultRest(req.url, req.method);
      });

    TestBed.configureTestingModule({
      providers: [
        FormBuilder,
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => routeId } } },
        },
        { provide: Router, useValue: router },
        { provide: HttpClient, useValue: { get: httpGet } },
        {
          provide: ConfigStateService,
          useValue: { getOne: () => currentUser, getAll: () => ({}) },
        },
        {
          provide: AppointmentService,
          useValue: {
            getWithNavigationProperties: getWithNav,
            update: updateAppointment,
            getAppointmentCustomFieldValues: getCustomFieldValues,
          },
        },
        { provide: RestService, useValue: { request: restRequest } },
        { provide: EnvironmentService, useValue: { getApiUrl: () => 'https://api.test' } },
        { provide: ToasterService, useValue: toaster },
        { provide: LocalizationService, useValue: { instant: (k: string) => k } },
        { provide: ConfirmationService, useValue: { warn: confirmationWarn } },
        {
          provide: AppointmentChangeRequestService,
          useValue: { getActiveForAppointment },
        },
      ],
    });
    return TestBed.runInInjectionContext(() => new AppointmentViewComponent()) as unknown as Probe;
  }

  /** A loaded appointment, complete enough for save() to pass its guard. */
  function loadedAppointment(over: Record<string, unknown> = {}) {
    return {
      appointment: {
        id: 'appt-1',
        patientId: 'patient-1',
        identityUserId: 'identity-1',
        appointmentTypeId: 'type-1',
        locationId: 'loc-1',
        doctorAvailabilityId: 'avail-1',
        requestConfirmationNumber: 'C0001',
        concurrencyStamp: 'stamp-1',
        appointmentStatus: AppointmentStatusType.Pending,
        creatorId: 'user-1',
        ...over,
      },
      patient: { id: 'patient-1', firstName: 'Ada', lastName: 'Lovelace' },
      claimExaminer: { name: 'Grace Hopper' },
      primaryInsurance: { name: 'Statewide Mutual' },
    };
  }

  function withLoaded(c: Probe, over: Record<string, unknown> = {}): void {
    c.appointment = loadedAppointment(over);
  }

  afterEach(() => TestBed.resetTestingModule());

  // ---------------------------------------------------------------- load

  describe('ngOnInit', () => {
    it('refuses to load without a route id', () => {
      const c = create({ id: null });
      c.ngOnInit();
      expect(c.errorMessage).toBe('Appointment id is required.');
      expect(c.isLoading).toBeFalse();
      expect(getWithNav).not.toHaveBeenCalled();
    });

    it('loads the appointment and patient into the form', () => {
      const c = create();
      getWithNav.and.returnValue(
        of(loadedAppointment({ panelNumber: 'PN-0001', refferedBy: 'Intake' })),
      );

      c.ngOnInit();

      expect(c.appointment).not.toBeNull();
      expect(c.form.get('patientFirstName')?.value).toBe('Ada');
      expect(c.form.get('patientLastName')?.value).toBe('Lovelace');
      expect(c.form.get('patientRefferedBy')?.value).toBe('Intake');
      expect(c.isLoading).toBeFalse();
    });

    it('never prefills the social security number', () => {
      /**
       * Design B: the stored SSN is viewed through the reveal endpoint, and an empty submit
       * leaves it unchanged. A REMOVAL that needs the value PRESENT in the response, or the
       * assertion would hold with the override deleted.
       */
      const c = create();
      const data = loadedAppointment();
      (data.patient as Record<string, unknown>)['socialSecurityNumber'] = SSN_PLACEHOLDER;
      getWithNav.and.returnValue(of(data));

      c.ngOnInit();

      expect(c.form.get('patientSocialSecurityNumber')?.value).toBe('');
    });

    it('surfaces the claim examiner and insurance names', () => {
      const c = create();
      getWithNav.and.returnValue(of(loadedAppointment()));
      c.ngOnInit();
      expect(c.appointmentClaimExaminerName).toBe('Grace Hopper');
      expect(c.appointmentInsuranceCompanyName).toBe('Statewide Mutual');
    });

    it('reports a failed load without leaving the spinner up', () => {
      const c = create();
      getWithNav.and.returnValue(throwError(() => ({ status: 500 })));

      c.ngOnInit();

      expect(c.errorMessage).toBe('Unable to load appointment details.');
      expect(c.isLoading).toBeFalse();
    });

    it('locks the whole form for an external viewer', () => {
      // O5 strict parity: booking is the canonical create surface, so external roles read
      // rather than edit. The server permission attributes remain authoritative.
      const c = create({ roles: ['Patient'] });
      getWithNav.and.returnValue(of(loadedAppointment()));

      c.ngOnInit();

      expect(c.form.disabled).toBeTrue();
    });

    it('leaves the form editable for internal staff', () => {
      const c = create({ roles: ['Staff Supervisor'] });
      getWithNav.and.returnValue(of(loadedAppointment()));

      c.ngOnInit();

      expect(c.form.disabled).toBeFalse();
    });

    it('preloads the state names for the read-only sections', () => {
      const c = create();
      c.ngOnInit();
      expect(c.stateName('state-1')).toBe('California');
      expect(c.stateName('unknown')).toBe('');
      expect(c.stateName(null)).toBe('');
    });

    it('loads the custom-field values for the additional-details card', () => {
      const c = create();
      getCustomFieldValues.and.returnValue(of([{ fieldType: 1, value: 'x', fieldLabel: 'L' }]));
      c.ngOnInit();
      expect(c.customFieldDisplayValues.length).toBe(1);
    });

    it('hides the additional-details card when its fetch fails', () => {
      // Best-effort: a failure leaves the card empty, exactly as when the type defines none.
      const c = create();
      getCustomFieldValues.and.returnValue(throwError(() => ({ status: 403 })));
      c.ngOnInit();
      expect(c.customFieldDisplayValues).toEqual([]);
    });
  });

  describe('the Panel Number state machine', () => {
    it('enables and requires the field for a PQME appointment', () => {
      const c = create({ roles: ['Staff Supervisor'] });
      getWithNav.and.returnValue(
        of(loadedAppointment({ appointmentTypeId: 'a0a00002-0000-4000-9000-000000000002' })),
      );

      c.ngOnInit();

      expect(c.form.get('panelNumber')?.enabled).toBeTrue();
      expect(c.form.get('panelNumber')?.hasError('required')).toBeTrue();
    });

    it('clears a legacy panel number on a non-PQME appointment', () => {
      /**
       * A REMOVAL needing the value seeded. This cleans up rather than blocks: an AME that
       * somehow carries a panel number is corrected on edit instead of failing to save.
       */
      const c = create({ roles: ['Staff Supervisor'] });
      c.form.get('panelNumber')?.setValue('PN-0001');
      getWithNav.and.returnValue(of(loadedAppointment({ appointmentTypeId: 'type-ame' })));

      c.ngOnInit();

      expect(c.form.get('panelNumber')?.value).toBeNull();
      expect(c.form.get('panelNumber')?.disabled).toBeTrue();
    });

    it('keeps the field locked for an external viewer even on a PQME', () => {
      // The panel-number state is applied BEFORE the read-only gate, so the global disable
      // must still win. Reversing that order would hand a patient an editable field.
      const c = create({ roles: ['Patient'] });
      getWithNav.and.returnValue(
        of(loadedAppointment({ appointmentTypeId: 'a0a00002-0000-4000-9000-000000000002' })),
      );

      c.ngOnInit();

      expect(c.form.get('panelNumber')?.disabled).toBeTrue();
    });
  });

  // ---------------------------------------------------------------- roles

  describe('role gates', () => {
    it('treats an unresolved role as a non-patient for the patient-update URL', () => {
      const c = create({ roles: [] });
      expect(c.isExternalUserNonPatient).toBeTrue();
    });

    it('recognises a patient', () => {
      expect(create({ roles: ['Patient'] }).isExternalUserNonPatient).toBeFalse();
    });

    it('recognises each attorney role case-insensitively', () => {
      expect(create({ roles: ['APPLICANT ATTORNEY'] }).isApplicantAttorney).toBeTrue();
      TestBed.resetTestingModule();
      expect(create({ roles: ['Defense Attorney'] }).isDefenseAttorney).toBeTrue();
    });

    it('classifies all four external roles as external', () => {
      for (const role of ['Patient', 'Applicant Attorney', 'Defense Attorney', 'Claim Examiner']) {
        TestBed.resetTestingModule();
        const c = create({ roles: [role] });
        expect(c.isPatientUser).withContext(role).toBeTrue();
        expect(c.isReadOnly).withContext(role).toBeTrue();
        expect(c.isInternalUser).withContext(role).toBeFalse();
      }
    });

    it('classifies internal staff as internal and editable', () => {
      const c = create({ roles: ['Staff Supervisor'] });
      expect(c.isPatientUser).toBeFalse();
      expect(c.isReadOnly).toBeFalse();
      expect(c.isInternalUser).toBeTrue();
    });

    it('does NOT use isExternalUserNonPatient as a read-only gate', () => {
      /**
       * W-VIEW-10, and the reason the two getters both exist: isExternalUserNonPatient returns
       * TRUE for internal admins, so using it to gate editing would lock staff out of their own
       * edit screen. This pins the divergence that makes them non-interchangeable.
       */
      const c = create({ roles: ['Staff Supervisor'] });
      expect(c.isExternalUserNonPatient).toBeTrue();
      expect(c.isReadOnly).toBeFalse();
    });
  });

  describe('office actions', () => {
    it('offers approve and reject on a pending appointment', () => {
      const c = create({ roles: ['Staff Supervisor'] });
      withLoaded(c, { appointmentStatus: AppointmentStatusType.Pending });
      expect(c.canTakeOfficeAction).toBeTrue();
      expect(c.availableActions).toEqual(['approve', 'reject']);
    });

    it('offers no toolbar action on an approved appointment', () => {
      // Staff cancel and reschedule route through the change-request + consent flow; the
      // one-step direct cancel was removed.
      const c = create({ roles: ['Staff Supervisor'] });
      withLoaded(c, { appointmentStatus: AppointmentStatusType.Approved });
      expect(c.canTakeOfficeAction).toBeTrue();
      expect(c.availableActions).toEqual([]);
    });

    it('offers nothing to an external viewer', () => {
      const c = create({ roles: ['Patient'] });
      withLoaded(c);
      expect(c.canTakeOfficeAction).toBeFalse();
    });

    it('offers nothing before the appointment loads', () => {
      const c = create({ roles: ['Staff Supervisor'] });
      expect(c.canTakeOfficeAction).toBeFalse();
      expect(c.currentStatus).toBeUndefined();
    });

    it('opens the matching modal', () => {
      const c = create({ roles: ['Staff Supervisor'] });
      withLoaded(c);
      c.dispatchAction('approve');
      expect(c.approveModalVisible).toBeTrue();
      c.dispatchAction('reject');
      expect(c.rejectModalVisible).toBeTrue();
    });

    it('opens nothing when no appointment is loaded', () => {
      const c = create({ roles: ['Staff Supervisor'] });
      c.dispatchAction('approve');
      expect(c.approveModalVisible).toBeFalse();
    });

    it('patches the status from the modal result before re-fetching', () => {
      /**
       * S-7.2: the modal hands back the post-transition dto so the status pill flips on the
       * same change-detection cycle the modal closed in. Waiting for the re-fetch would leave
       * the old status visible for a round trip.
       */
      const c = create({ roles: ['Staff Supervisor'] });
      withLoaded(c, { appointmentStatus: AppointmentStatusType.Pending });
      getWithNav.and.returnValue(of(loadedAppointment()));

      c.onActionSucceeded({ appointmentStatus: AppointmentStatusType.Approved } as never);

      expect(getWithNav).toHaveBeenCalledWith('appt-1');
    });

    it('does nothing on a transition result with no loaded appointment', () => {
      const c = create({ roles: ['Staff Supervisor'] });
      c.onActionSucceeded({ id: 'x' } as never);
      expect(getWithNav).not.toHaveBeenCalled();
    });
  });

  describe('re-request and re-book', () => {
    it('offers re-request to the creator of a rejected appointment', () => {
      const c = create({ roles: ['Applicant Attorney'] });
      withLoaded(c, { appointmentStatus: AppointmentStatusType.Rejected, creatorId: 'user-1' });
      expect(c.canReRequest).toBeTrue();
    });

    it('refuses re-request to someone who did not create it', () => {
      // creatorId is the ABP audit author = the original booker. identityUserId is the
      // patient's user, so creatorId is the correct "did I file this" check.
      const c = create({ roles: ['Applicant Attorney'] });
      withLoaded(c, {
        appointmentStatus: AppointmentStatusType.Rejected,
        creatorId: 'someone-else',
      });
      expect(c.canReRequest).toBeFalse();
    });

    it('refuses re-request on a non-rejected appointment', () => {
      const c = create({ roles: ['Applicant Attorney'] });
      withLoaded(c, { appointmentStatus: AppointmentStatusType.Approved, creatorId: 'user-1' });
      expect(c.canReRequest).toBeFalse();
    });

    it('navigates to the booking form in re-request mode', () => {
      const c = create();
      withLoaded(c, { requestConfirmationNumber: 'C0009' });
      c.reRequest();
      expect(router.navigate).toHaveBeenCalledWith(['/appointments/request'], {
        queryParams: { mode: 'rerequest', source: 'C0009' },
      });
    });

    it('does not navigate without a confirmation number', () => {
      const c = create();
      withLoaded(c, { requestConfirmationNumber: null });
      c.reRequest();
      c.reBook();
      expect(router.navigate).not.toHaveBeenCalled();
    });

    it('offers re-book on status alone, not on authorship', () => {
      /**
       * Deliberately NOT the creator-compare re-request uses. The server's gate is READ access,
       * already proved by the call that loaded this page -- narrowing to the creator would hide
       * the button from an attorney on the case whose POST the server would accept.
       */
      const c = create({ roles: ['Applicant Attorney'] });
      withLoaded(c, {
        appointmentStatus: AppointmentStatusType.CancelledNoBill,
        creatorId: 'someone-else',
      });
      expect(c.canReBook).toBeTrue();
      expect(c.canReRequest).toBeFalse();
    });

    it('offers re-book from every status where the appointment did not happen', () => {
      // The whole rule: cancelled either billing tier, no-show, or not seen. An appointment
      // that HAPPENED is followed up with a re-evaluation instead, and a rejected request is
      // re-entered with a re-request -- three flows, three server endpoints.
      const c = create({ roles: ['Staff Supervisor'] });
      for (const status of [
        AppointmentStatusType.NoShow,
        AppointmentStatusType.NotSeen,
        AppointmentStatusType.CancelledNoBill,
        AppointmentStatusType.CancelledLate,
      ]) {
        withLoaded(c, { appointmentStatus: status });
        expect(c.canReBook).withContext(AppointmentStatusType[status]).toBeTrue();
      }
    });

    it('refuses re-book on an appointment that took place', () => {
      const c = create({ roles: ['Staff Supervisor'] });
      for (const status of [
        AppointmentStatusType.Approved,
        AppointmentStatusType.CheckedOut,
        AppointmentStatusType.Billed,
        AppointmentStatusType.Rejected,
      ]) {
        withLoaded(c, { appointmentStatus: status });
        expect(c.canReBook).withContext(AppointmentStatusType[status]).toBeFalse();
      }
    });

    it('navigates to the booking form in re-book mode', () => {
      const c = create();
      withLoaded(c, { requestConfirmationNumber: 'C0009' });
      c.reBook();
      expect(router.navigate).toHaveBeenCalledWith(['/appointments/request'], {
        queryParams: { type: 3, source: 'C0009' },
      });
    });
  });

  describe('change requests', () => {
    it('offers the request buttons to an external viewer on an approved appointment', () => {
      const c = create({ roles: ['Patient'] });
      withLoaded(c, { appointmentStatus: AppointmentStatusType.Approved });
      expect(c.canRequestChange).toBeTrue();
    });

    it('refuses them to internal staff, who use the list dropdown', () => {
      const c = create({ roles: ['Staff Supervisor'] });
      withLoaded(c, { appointmentStatus: AppointmentStatusType.Approved });
      expect(c.canRequestChange).toBeFalse();
    });

    it('hides them once a cancel request is already pending', () => {
      /**
       * C4: a pending cancel leaves the parent APPROVED, so status alone cannot reveal it.
       * The fixture therefore seeds an active pending cancel -- without it the guard is
       * unreachable and this passes with the check deleted.
       */
      const c = create({ roles: ['Patient'] });
      withLoaded(c, { appointmentStatus: AppointmentStatusType.Approved });
      c.activeChangeRequest = {
        changeRequestType: ChangeRequestType.Cancel,
        requestStatus: RequestStatusType.Pending,
      };

      expect(c.hasPendingCancelRequest).toBeTrue();
      expect(c.canRequestChange).toBeFalse();
    });

    it('keeps them available while a RESCHEDULE request is pending', () => {
      const c = create({ roles: ['Patient'] });
      withLoaded(c, { appointmentStatus: AppointmentStatusType.Approved });
      c.activeChangeRequest = {
        changeRequestType: ChangeRequestType.Reschedule,
        requestStatus: RequestStatusType.Pending,
      };
      expect(c.hasPendingCancelRequest).toBeFalse();
    });

    it('opens one request panel at a time', () => {
      const c = create({ roles: ['Patient'] });
      c.openRescheduleRequest();
      expect(c.rescheduleRequestVisible).toBeTrue();
      expect(c.cancelRequestVisible).toBeFalse();

      c.openCancelRequest();
      expect(c.cancelRequestVisible).toBeTrue();
      expect(c.rescheduleRequestVisible).toBeFalse();
    });

    it('has no consent indicator without an active request', () => {
      const c = create();
      c.activeChangeRequest = null;
      expect(c.consentIndicator).toBeNull();
    });

    it('loads the active change request for the appointment', () => {
      const c = create();
      getActiveForAppointment.and.returnValue(of({ id: 'cr-1' }));
      c.loadActiveChangeRequest('appt-1');
      expect(c.activeChangeRequest).toEqual({ id: 'cr-1' });
    });

    it('clears the active request when there is none or the fetch fails', () => {
      const c = create();
      c.activeChangeRequest = { id: 'stale' };
      c.loadActiveChangeRequest(undefined);
      expect(c.activeChangeRequest).toBeNull();

      c.activeChangeRequest = { id: 'stale' };
      getActiveForAppointment.and.returnValue(throwError(() => ({ status: 500 })));
      c.loadActiveChangeRequest('appt-1');
      expect(c.activeChangeRequest).toBeNull();
    });

    it('confirms a filed request and refreshes the appointment', () => {
      const c = create({ roles: ['Patient'] });
      withLoaded(c);
      getWithNav.and.returnValue(of(loadedAppointment()));

      c.onChangeRequestSucceeded({ changeRequestType: ChangeRequestType.Cancel } as never);

      expect(toaster.success).toHaveBeenCalledWith('::Appointment:Toast:CancelRequested');
      expect(getWithNav).toHaveBeenCalledWith('appt-1');
      expect(getActiveForAppointment).toHaveBeenCalledWith('appt-1');
    });

    it('uses the reschedule wording for a reschedule request', () => {
      const c = create({ roles: ['Patient'] });
      withLoaded(c);
      c.onChangeRequestSucceeded({ changeRequestType: ChangeRequestType.Reschedule } as never);
      expect(toaster.success).toHaveBeenCalledWith('::Appointment:Toast:RescheduleRequested');
    });
  });

  describe('request more information', () => {
    it('is offered to staff on a pending appointment only', () => {
      const c = create({ roles: ['Staff Supervisor'] });
      withLoaded(c, { appointmentStatus: AppointmentStatusType.Pending });
      expect(c.canRequestInfo).toBeTrue();

      withLoaded(c, { appointmentStatus: AppointmentStatusType.Approved });
      expect(c.canRequestInfo).toBeFalse();
    });

    it('is not offered to external viewers', () => {
      const c = create({ roles: ['Patient'] });
      withLoaded(c, { appointmentStatus: AppointmentStatusType.Pending });
      expect(c.canRequestInfo).toBeFalse();
    });

    it('opens the modal and refreshes after a successful send-back', () => {
      const c = create({ roles: ['Staff Supervisor'] });
      withLoaded(c);
      c.openRequestInfo();
      expect(c.requestInfoModalVisible).toBeTrue();

      getWithNav.and.returnValue(of(loadedAppointment()));
      c.onInfoRequestSucceeded();
      expect(getWithNav).toHaveBeenCalledWith('appt-1');
    });

    it('does not refresh when no appointment is loaded', () => {
      const c = create({ roles: ['Staff Supervisor'] });
      c.onInfoRequestSucceeded();
      expect(getWithNav).not.toHaveBeenCalled();
    });
  });

  // ---------------------------------------------------------------- reschedule chain

  describe('the rescheduled-from block', () => {
    it('reports no source when the chain is absent', () => {
      const c = create();
      withLoaded(c);
      expect(c.hasRescheduleSource).toBeFalse();
      expect(c.rescheduleSourceId).toBeNull();
    });

    it('exposes the source id when the chain carries one', () => {
      const c = create();
      withLoaded(c);
      c.appointment.rescheduleChain = { sourceAppointmentId: 'appt-0' };
      expect(c.rescheduleSourceId).toBe('appt-0');
    });

    it('returns the SAME array instance across repeated reads', () => {
      /**
       * Memoized on the chain's object identity. A getter that allocates a fresh array each
       * change-detection pass is what hung the tab for two hours in phase 4b -- and silently,
       * because the containers serve a production build with Angular's loop guard compiled out.
       */
      const c = create();
      withLoaded(c);
      c.appointment.rescheduleChain = { sourceAppointmentId: 'appt-0' };

      const first = c.rescheduleChainSteps;
      const second = c.rescheduleChainSteps;

      expect(second).toBe(first);
    });

    it('recomputes when the chain object changes', () => {
      const c = create();
      withLoaded(c);
      c.appointment.rescheduleChain = { sourceAppointmentId: 'appt-0' };
      const first = c.rescheduleChainSteps;

      c.appointment.rescheduleChain = { sourceAppointmentId: 'appt-9' };

      expect(c.rescheduleChainSteps).not.toBe(first);
    });

    /**
     * NOT TESTED, deliberately: that `openRescheduleSource` calls
     * `window.location.assign('/appointments/view/<id>')` on the happy path.
     *
     * `window.location.assign` is [Unforgeable] -- jasmine refuses with "assign is not declared
     * writable or has no setter", and because the spy never installs, the REAL navigation runs.
     * In karma that navigates the test page away: the first version of this spec disconnected
     * the browser ("no message in 30000 ms") and aborted the run at 549 of 682 tests, so it did
     * not merely fail, it took the suite with it.
     *
     * Testing it properly needs an injectable navigation seam in the component, which is a
     * production change and does not belong in a coverage PR. The two halves that CAN be
     * asserted safely are covered: the source id resolution above, and the guard below (which
     * returns before touching location). Logged to docs/backlog.md.
     */
    it('does nothing when there is no source', () => {
      // Safe precisely because the guard returns before reaching window.location -- this asserts
      // that the guard exists at all. If it were removed, this test would navigate and kill the
      // run, which is a loud failure rather than a silent one.
      const c = create();
      withLoaded(c);
      expect(c.rescheduleSourceId).toBeNull();
      expect(() => c.openRescheduleSource()).not.toThrow();
      expect(router.navigate).not.toHaveBeenCalled();
    });
  });

  // ---------------------------------------------------------------- save

  describe('save', () => {
    function ready(roles = ['Staff Supervisor']): Probe {
      const c = create({ roles });
      withLoaded(c);
      c.form.patchValue({
        patientFirstName: 'Ada',
        patientLastName: 'Lovelace',
        patientEmail: 'ada@example.test',
      });
      return c;
    }

    it('refuses a second save while one is in flight', async () => {
      // Mirrors the booker's re-entrancy guard: the [disabled] binding only applies after
      // change detection, leaving a click window that re-fired the whole chain.
      const c = ready();
      c.isSaving = true;
      await c.save();
      expect(restRequest).not.toHaveBeenCalled();
    });

    it('rejects when the appointment is missing required identifiers', async () => {
      const c = create({ roles: ['Staff Supervisor'] });
      withLoaded(c, { locationId: null });

      await expectAsync(c.save()).toBeRejected();
      expect(c.errorMessage).toBe('Appointment data is incomplete and cannot be saved.');
    });

    it('updates the patient then the appointment, and reports success', async () => {
      const c = ready();

      await c.save();

      const urls = restRequest.calls.allArgs().map(([req]) => `${req.method} ${req.url}`);
      expect(urls.some((u: string) => u.startsWith('PUT /api/app/patients/'))).toBeTrue();
      expect(updateAppointment).toHaveBeenCalled();
      expect(c.successMessage).toContain('updated successfully');
      expect(c.isSaving).toBeFalse();
    });

    it('sends a patient booker to the self endpoint', () => {
      // A patient edits their own record via /patients/me; the booking endpoint 404s for them.
      const c = ready(['Patient']);
      void c.save();
      const urls = restRequest.calls.allArgs().map(([req]) => req.url);
      expect(urls.some((u: string) => u.includes('/api/app/patients/me'))).toBeTrue();
    });

    it('sends a staff booker to the on-behalf endpoint', () => {
      const c = ready(['Staff Supervisor']);
      void c.save();
      const urls = restRequest.calls.allArgs().map(([req]) => req.url);
      expect(
        urls.some((u: string) => u.includes('/api/app/patients/for-appointment-booking/patient-1')),
      ).toBeTrue();
    });

    it('sends only the fields the appointment update contract accepts', async () => {
      /**
       * The backend UpdateAsync accepts exactly these; appointmentStatus, internalUserComments
       * and isPatientAlreadyExist are set by dedicated transitions and must NOT ride along.
       * The earlier view sent them and TypeScript silently dropped them.
       */
      const c = ready();

      await c.save();

      const payload = updateAppointment.calls.mostRecent().args[1];
      expect(payload.requestConfirmationNumber).toBe('C0001');
      expect(payload.concurrencyStamp).toBe('stamp-1');
      expect(payload.appointmentStatus).toBeUndefined();
      expect(payload.internalUserComments).toBeUndefined();
    });

    it('rejects and explains when the patient update fails', async () => {
      const c = ready();
      restResponses['/api/app/patients/'] = throwError(() => ({ status: 500 }));

      await expectAsync(c.save()).toBeRejected();

      expect(c.errorMessage).toBe('Failed to save patient details.');
      expect(c.isSaving).toBeFalse();
      expect(updateAppointment).not.toHaveBeenCalled();
    });

    it('distinguishes a failed appointment save from a failed patient save', async () => {
      // The message names which half succeeded, because the patient edit is already committed
      // at that point and retrying the whole form would re-apply it.
      const c = ready();
      updateAppointment.and.returnValue(throwError(() => ({ status: 500 })));

      await expectAsync(c.save()).toBeRejected();

      expect(c.errorMessage).toBe('Patient updated, but appointment save failed.');
      expect(c.isSaving).toBeFalse();
    });

    it('reports a downstream failure rather than swallowing it', async () => {
      /**
       * 2026-08-06: a 403 here means a missing permission and retrying can never work, which
       * is the opposite advice from a transient failure. The old single generic message gave
       * neither, and the catch swallowed the cause entirely.
       */
      const c = ready();
      restResponses['/applicant-attorney'] = throwError(() => ({ status: 403 }));
      c.form.patchValue({
        applicantAttorneyEnabled: true,
        applicantAttorneyIdentityUserId: 'identity-9',
      });

      await expectAsync(c.save()).toBeRejected();

      expect(c.errorMessage).not.toBe('');
      expect(c.successMessage).toBe('');
      expect(c.isSaving).toBeFalse();
    });

    it('releases the saving flag on every path', async () => {
      const c = ready();
      await c.save();
      expect(c.isSaving).toBeFalse();
    });
  });

  describe('employer details', () => {
    it('loads the employer block into the form', () => {
      const c = create({
        rest: {
          'appointment-employer-details': of({
            items: [
              {
                appointmentEmployerDetail: {
                  id: 'emp-1',
                  employerName: 'Acme Manufacturing',
                  occupation: 'Machinist',
                  city: 'Encino',
                  concurrencyStamp: 'emp-stamp',
                },
              },
            ],
          }),
        },
      });

      c.loadEmployerDetails('appt-1');

      expect(c.employerDetailId).toBe('emp-1');
      expect(c.employerDetailConcurrencyStamp).toBe('emp-stamp');
      expect(c.form.get('employerName')?.value).toBe('Acme Manufacturing');
      expect(c.form.get('employerCity')?.value).toBe('Encino');
    });

    it('does nothing without an appointment id or an employer row', () => {
      const c = create();
      c.loadEmployerDetails(undefined);
      expect(c.employerDetailId).toBeNull();

      c.loadEmployerDetails('appt-1');
      expect(c.employerDetailId).toBeNull();
    });

    it('reports no employer data on a blank form', () => {
      expect(create().hasEmployerData()).toBeFalse();
    });

    it('reports employer data when any field is filled', () => {
      const c = create();
      c.form.patchValue({ employerCity: 'Encino' });
      expect(c.hasEmployerData()).toBeTrue();
    });

    it('does not post an employer row when nothing was entered', async () => {
      const c = create();
      restRequest.calls.reset();
      await c.upsertEmployerDetails('appt-1');
      expect(restRequest).not.toHaveBeenCalled();
    });

    it('creates an employer row when none exists', async () => {
      const c = create({
        rest: { 'appointment-employer-details': of({ id: 'emp-new', concurrencyStamp: 's2' }) },
      });
      c.form.patchValue({ employerName: 'Acme Manufacturing', employerOccupation: 'Machinist' });

      await c.upsertEmployerDetails('appt-1');

      expect(c.employerDetailId).toBe('emp-new');
      expect(c.employerDetailConcurrencyStamp).toBe('s2');
    });

    it('updates in place when a row already exists', async () => {
      const c = create({
        rest: { 'appointment-employer-details': of({ concurrencyStamp: 's3' }) },
      });
      c.employerDetailId = 'emp-1';
      c.form.patchValue({ employerName: 'Acme Manufacturing', employerOccupation: 'Machinist' });

      await c.upsertEmployerDetails('appt-1');

      const methods = restRequest.calls.allArgs().map(([req]) => req.method);
      expect(methods).toContain('PUT');
      expect(c.employerDetailConcurrencyStamp).toBe('s3');
    });

    it('refuses to create a row without both required fields', async () => {
      // Name and occupation are required by the create contract; a partial row would 400.
      const c = create();
      c.form.patchValue({ employerCity: 'Encino' });
      restRequest.calls.reset();

      await c.upsertEmployerDetails('appt-1');

      expect(restRequest.calls.allArgs().some(([req]) => req.method === 'POST')).toBeFalse();
    });
  });

  describe('attorney lookups', () => {
    const lookup = {
      applicantAttorneyId: 'aa-1',
      defenseAttorneyId: 'da-1',
      identityUserId: 'identity-9',
      firstName: 'Grace',
      lastName: 'Hopper',
      email: 'grace@example.test',
      firmName: 'Hopper & Co',
      city: 'Encino',
    };

    it('does not search on a blank email', () => {
      const c = create();
      restRequest.calls.reset();
      c.loadApplicantAttorneyByEmail();
      c.loadDefenseAttorneyByEmail();
      expect(restRequest).not.toHaveBeenCalled();
      expect(c.isApplicantAttorneyLoading).toBeFalse();
    });

    it('fills the applicant section from an email search', () => {
      const c = create({ rest: { 'applicant-attorney-details-for-booking': of(lookup) } });
      c.form.get('applicantAttorneyEmailSearch')?.setValue(' grace@example.test ');

      c.loadApplicantAttorneyByEmail();

      expect(c.applicantAttorneyId).toBe('aa-1');
      expect(c.form.get('applicantAttorneyFirstName')?.value).toBe('Grace');
      expect(c.form.get('applicantAttorneyFirmName')?.value).toBe('Hopper & Co');
      expect(c.isApplicantAttorneyLoading).toBeFalse();
    });

    it('releases the loading flag when the lookup fails', () => {
      const c = create({
        rest: { 'applicant-attorney-details-for-booking': throwError(() => ({ status: 500 })) },
      });
      c.form.get('applicantAttorneyEmailSearch')?.setValue('grace@example.test');

      c.loadApplicantAttorneyByEmail();

      expect(c.isApplicantAttorneyLoading).toBeFalse();
    });

    it('ignores a null id on select', () => {
      const c = create();
      restRequest.calls.reset();
      c.onApplicantAttorneySelected(null);
      c.onDefenseAttorneySelected(null);
      expect(restRequest).not.toHaveBeenCalled();
    });

    it('fills the applicant section from a selected identity', () => {
      const c = create({ rest: { 'applicant-attorney-details-for-booking': of(lookup) } });
      c.onApplicantAttorneySelected('identity-9');
      expect(c.form.get('applicantAttorneyEmail')?.value).toBe('grace@example.test');
    });

    it('mirrors the behaviour for the defense section', () => {
      const c = create({ rest: { 'defense-attorney-details-for-booking': of(lookup) } });
      c.form.get('defenseAttorneyEmailSearch')?.setValue('alan@example.test');

      c.loadDefenseAttorneyByEmail();

      expect(c.defenseAttorneyId).toBe('da-1');
      expect(c.form.get('defenseAttorneyFirstName')?.value).toBe('Grace');
      expect(c.isDefenseAttorneyLoading).toBeFalse();
    });

    it('releases the defense loading flag on failure', () => {
      const c = create({
        rest: { 'defense-attorney-details-for-booking': throwError(() => ({ status: 500 })) },
      });
      c.onDefenseAttorneySelected('identity-9');
      expect(c.isDefenseAttorneyLoading).toBeFalse();
    });

    it('writes the search box and loads the record on a typeahead pick', () => {
      const c = create({ rest: { 'applicant-attorney-details-for-booking': of(lookup) } });
      const event = {
        preventDefault: jasmine.createSpy('preventDefault'),
        item: { identityUserId: 'identity-9', email: 'grace@example.test' },
      };

      c.onApplicantAttorneyTypeaheadSelect(event);

      expect(event.preventDefault).toHaveBeenCalled();
      expect(c.form.get('applicantAttorneyEmailSearch')?.value).toBe('grace@example.test');
      expect(c.form.get('applicantAttorneyFirstName')?.value).toBe('Grace');
    });

    it('does the same for the defense typeahead', () => {
      const c = create({ rest: { 'defense-attorney-details-for-booking': of(lookup) } });
      const event = {
        preventDefault: jasmine.createSpy('preventDefault'),
        item: { identityUserId: 'identity-9', email: 'alan@example.test' },
      };

      c.onDefenseAttorneyTypeaheadSelect(event);

      expect(c.form.get('defenseAttorneyEmailSearch')?.value).toBe('alan@example.test');
      expect(c.form.get('defenseAttorneyFirstName')?.value).toBe('Grace');
    });

    it('labels a picker option with the display name and email', () => {
      const c = create();
      expect(
        c.applicantAttorneyOptionLabel({
          firstName: 'Grace',
          lastName: 'Hopper',
          email: 'grace@example.test',
        }),
      ).toBe('Grace Hopper (grace@example.test)');
    });

    it('falls back to the firm name for a firm account', () => {
      // A firm account has no personal name; showing a raw email instead of the firm name is
      // what this resolution exists to prevent.
      const c = create();
      expect(
        c.applicantAttorneyOptionLabel({
          firstName: '',
          lastName: '',
          firmName: 'Hopper & Co',
          email: 'office@example.test',
        }),
      ).toBe('Hopper & Co (office@example.test)');
    });

    it('does not repeat the email when it IS the display name', () => {
      const c = create();
      expect(
        c.applicantAttorneyOptionLabel({ firstName: '', lastName: '', email: 'x@example.test' }),
      ).toBe('x@example.test');
    });

    it('formats the typeahead result and input', () => {
      const c = create();
      const opt = { firstName: 'Grace', lastName: 'Hopper', email: 'grace@example.test' };
      expect(c.formatAttorneyResult(opt)).toContain('Grace Hopper');
      expect(c.formatAttorneyInput(opt)).toBe('grace@example.test');
      expect(c.formatAttorneyInput('typed text')).toBe('typed text');
    });
  });

  describe('authorized users', () => {
    const row = {
      accessorId: 'acc-1',
      identityUserId: 'identity-9',
      firstName: 'Grace',
      lastName: 'Hopper',
      email: 'grace@example.test',
      userRole: 'Applicant Attorney',
      accessTypeId: 24,
    };

    it('may be managed by internal staff', () => {
      const c = create({ roles: ['Staff Supervisor'] });
      withLoaded(c);
      expect(c.canManageAccessors()).toBeTrue();
    });

    it('may be managed by the attorney who created the appointment', () => {
      const c = create({ roles: ['Applicant Attorney'] });
      withLoaded(c, { creatorId: 'user-1' });
      expect(c.canManageAccessors()).toBeTrue();
    });

    it('may not be managed by an attorney who did not create it', () => {
      const c = create({ roles: ['Applicant Attorney'] });
      withLoaded(c, { creatorId: 'someone-else' });
      expect(c.canManageAccessors()).toBeFalse();
    });

    it('may not be managed by a patient', () => {
      const c = create({ roles: ['Patient'] });
      withLoaded(c, { creatorId: 'user-1' });
      expect(c.canManageAccessors()).toBeFalse();
    });

    it('opens the add modal with every identity field editable', () => {
      const c = create({ roles: ['Staff Supervisor'] });
      c.openAddAuthorizedUserModal();

      expect(c.authorizedUserModalMode).toBe('create');
      expect(c.editingAuthorizedUserId).toBeNull();
      expect(c.isAuthorizedUserModalOpen).toBeTrue();
      expect(c.authorizedUserForm.get('email')?.enabled).toBeTrue();
      expect(c.authorizedUserForm.get('accessTypeId')?.value).toBe(23);
    });

    it('opens the edit modal with identity locked and rights editable', () => {
      /**
       * The update contract changes only the rights -- the person is fixed. Leaving the
       * identity editable would let someone retype the email and silently repoint the row at
       * a different user.
       */
      const c = create({ roles: ['Staff Supervisor'] });
      c.openEditAuthorizedUserModal(row);

      expect(c.authorizedUserModalMode).toBe('edit');
      expect(c.editingAuthorizedUserId).toBe('acc-1');
      expect(c.authorizedUserForm.get('email')?.disabled).toBeTrue();
      expect(c.authorizedUserForm.get('userRole')?.disabled).toBeTrue();
      expect(c.authorizedUserForm.get('accessTypeId')?.enabled).toBeTrue();
      expect(c.authorizedUserForm.get('accessTypeId')?.value).toBe(24);
    });

    it('re-enables the identity fields when switching back to add', () => {
      /**
       * A REMOVAL needing the disabled state seeded: open Edit (which disables them), then
       * Add. Without the re-enable the create form would be unfillable -- and a disabled
       * control skips its required validator, so it would submit blank instead.
       */
      const c = create({ roles: ['Staff Supervisor'] });
      c.openEditAuthorizedUserModal(row);
      expect(c.authorizedUserForm.get('email')?.disabled).toBeTrue();

      c.openAddAuthorizedUserModal();

      expect(c.authorizedUserForm.get('email')?.enabled).toBeTrue();
      expect(c.authorizedUserForm.get('userRole')?.enabled).toBeTrue();
    });

    it('closes the modal', () => {
      const c = create();
      c.isAuthorizedUserModalOpen = true;
      c.closeAuthorizedUserModal();
      expect(c.isAuthorizedUserModalOpen).toBeFalse();
    });

    it('refuses to save an invalid draft and marks it touched', async () => {
      const c = create({ roles: ['Staff Supervisor'] });
      withLoaded(c);
      c.openAddAuthorizedUserModal();
      restRequest.calls.reset();

      await c.saveAuthorizedUserFromModal();

      expect(c.authorizedUserForm.get('email')?.touched).toBeTrue();
      expect(restRequest.calls.allArgs().some(([r]) => r.method === 'POST')).toBeFalse();
    });

    it('does nothing without a loaded appointment', async () => {
      const c = create({ roles: ['Staff Supervisor'] });
      restRequest.calls.reset();
      await c.saveAuthorizedUserFromModal();
      expect(restRequest).not.toHaveBeenCalled();
    });

    it('creates an accessor from the typed email', async () => {
      const c = create({ roles: ['Staff Supervisor'] });
      withLoaded(c);
      c.openAddAuthorizedUserModal();
      // The email is NOT padded here. `Validators.email` rejects surrounding whitespace, so a
      // padded address makes the whole form invalid and the save returns before any POST --
      // the component's own `.trim()` on the email is therefore unreachable through the form.
      // Only the name fields can carry padding this far, so they are what the trim is shown on.
      c.authorizedUserForm.patchValue({
        email: 'grace@example.test',
        firstName: ' Grace ',
        lastName: ' Hopper ',
        userRole: 'Applicant Attorney',
        accessTypeId: 23,
      });

      await c.saveAuthorizedUserFromModal();

      const post = restRequest.calls
        .allArgs()
        .map(([r]) => r)
        .find((r: any) => r.method === 'POST');
      expect(post).toBeDefined();
      expect(post.body.email).toBe('grace@example.test');
      expect(post.body.firstName).toBe('Grace');
      expect(post.body.lastName).toBe('Hopper');
      expect(c.isAuthorizedUserModalOpen).toBeFalse();
    });

    it('refuses a duplicate email rather than creating a second row', async () => {
      /**
       * A REMOVAL that needs the existing accessor seeded -- against an empty list the dedup
       * is a no-op and would pass with the check deleted. The comparison is case-insensitive
       * because the email is the accessor's identity key.
       */
      const c = create({ roles: ['Staff Supervisor'] });
      withLoaded(c);
      c.appointmentAuthorizedUsers = [row];
      c.openAddAuthorizedUserModal();
      c.authorizedUserForm.patchValue({
        email: 'GRACE@EXAMPLE.TEST',
        userRole: 'Applicant Attorney',
      });
      restRequest.calls.reset();

      await c.saveAuthorizedUserFromModal();

      expect(restRequest.calls.allArgs().some(([r]) => r.method === 'POST')).toBeFalse();
      expect(c.isAuthorizedUserModalOpen).toBeTrue();
    });

    it('updates only the rights when editing', async () => {
      const c = create({ roles: ['Staff Supervisor'] });
      withLoaded(c);
      c.openEditAuthorizedUserModal(row);
      c.authorizedUserForm.patchValue({ accessTypeId: 23 });

      await c.saveAuthorizedUserFromModal();

      const put = restRequest.calls
        .allArgs()
        .map(([r]) => r)
        .find((r: any) => r.method === 'PUT');
      expect(put.url).toContain('acc-1');
      expect(put.body.accessTypeId).toBe(23);
      expect(put.body.identityUserId).toBe('identity-9');
    });

    it('confirms before removing an accessor', () => {
      // QA item 14: removal is destructive -- they lose access -- and it used to delete on a
      // single click.
      const c = create({ roles: ['Staff Supervisor'] });
      withLoaded(c);
      c.appointmentAuthorizedUsers = [row];

      void c.removeAuthorizedUser(row);

      expect(confirmationWarn).toHaveBeenCalled();
      expect(confirmationWarn.calls.mostRecent().args[0]).toContain('Grace Hopper');
    });

    it('removes the row once confirmed', async () => {
      const c = create({ roles: ['Staff Supervisor'], confirmStatus: Confirmation.Status.confirm });
      withLoaded(c);
      c.appointmentAuthorizedUsers = [row];

      await c.removeAuthorizedUser(row);

      expect(restRequest.calls.allArgs().some(([r]) => r.method === 'DELETE')).toBeTrue();
      expect(c.appointmentAuthorizedUsers).toEqual([]);
    });

    it('keeps the row when the confirmation is declined', async () => {
      const c = create({ roles: ['Staff Supervisor'], confirmStatus: Confirmation.Status.reject });
      withLoaded(c);
      c.appointmentAuthorizedUsers = [row];
      restRequest.calls.reset();

      await c.removeAuthorizedUser(row);

      expect(restRequest.calls.allArgs().some(([r]) => r.method === 'DELETE')).toBeFalse();
      expect(c.appointmentAuthorizedUsers.length).toBe(1);
    });

    it('ignores a removal with no accessor id', async () => {
      const c = create({ roles: ['Staff Supervisor'] });
      await c.removeAuthorizedUser({ accessorId: '' } as never);
      expect(confirmationWarn).not.toHaveBeenCalled();
    });

    it('maps the accessor rows from the server response', () => {
      const c = create({
        rest: {
          'appointment-accessors': of({
            items: [
              {
                appointmentAccessor: {
                  id: 'acc-1',
                  identityUserId: 'identity-9',
                  accessTypeId: '24',
                },
                identityUser: { name: 'Grace', surname: 'Hopper', email: 'grace@example.test' },
                userRoleName: 'Applicant Attorney',
              },
            ],
          }),
        },
      });

      c.loadAppointmentAccessors('appt-1');

      expect(c.appointmentAuthorizedUsers.length).toBe(1);
      expect(c.appointmentAuthorizedUsers[0].firstName).toBe('Grace');
      expect(c.appointmentAuthorizedUsers[0].accessTypeId).toBe(24);
      expect(c.appointmentAuthorizedUsers[0].userRole).toBe('Applicant Attorney');
    });

    it('prefers the server-resolved role over the client lookup', () => {
      // The backend always populates it; the client lookup is only a fallback.
      const c = create({
        rest: {
          'appointment-accessors': of({
            items: [
              {
                appointmentAccessor: { id: 'acc-1', identityUserId: 'identity-9' },
                userRoleName: 'Defense Attorney',
              },
            ],
          }),
        },
      });
      c.externalAuthorizedUserOptions = [
        { identityUserId: 'identity-9', userRole: 'Applicant Attorney' },
      ];

      c.loadAppointmentAccessors('appt-1');

      expect(c.appointmentAuthorizedUsers[0].userRole).toBe('Defense Attorney');
    });

    it('clears the list when there is no appointment', () => {
      const c = create();
      c.appointmentAuthorizedUsers = [row];
      c.loadAppointmentAccessors(undefined);
      expect(c.appointmentAuthorizedUsers).toEqual([]);
    });

    it('backfills a blank role once the lookup arrives', () => {
      /**
       * A REMOVAL needing the BLANK seeded: the accessor rows can be built before the
       * external-user lookup resolves, leaving userRole empty. With a populated role the
       * backfill is a no-op and proves nothing.
       */
      const c = create();
      c.appointmentAuthorizedUsers = [{ ...row, userRole: '' }];
      c.externalAuthorizedUserOptions = [
        { identityUserId: 'identity-9', userRole: 'Applicant Attorney' },
      ];

      c.refreshAuthorizedUserRoles();

      expect(c.appointmentAuthorizedUsers[0].userRole).toBe('Applicant Attorney');
    });

    it('does not overwrite a role that is already resolved', () => {
      const c = create();
      c.appointmentAuthorizedUsers = [{ ...row, userRole: 'Claim Examiner' }];
      c.externalAuthorizedUserOptions = [{ identityUserId: 'identity-9', userRole: '' }];

      c.refreshAuthorizedUserRoles();

      expect(c.appointmentAuthorizedUsers[0].userRole).toBe('Claim Examiner');
    });

    it('does nothing when either list is empty', () => {
      const c = create();
      c.appointmentAuthorizedUsers = [];
      expect(() => c.refreshAuthorizedUserRoles()).not.toThrow();
    });

    it('labels the access types', () => {
      const c = create();
      expect(c.getAccessTypeLabel(23)).toBe('View');
      expect(c.getAccessTypeLabel(24)).toBe('Edit');
      expect(c.getAccessTypeLabel(99)).toBe('');
    });

    it('splits the external lookup into per-role option lists', () => {
      const c = create({
        rest: {
          'external-user-lookup': of({
            items: [
              { identityUserId: 'u1', userRole: 'Applicant Attorney' },
              { identityUserId: 'u2', userRole: 'Defense Attorney' },
              { identityUserId: 'u3', userRole: 'Patient' },
            ],
          }),
        },
      });

      c.loadExternalAuthorizedUsers();

      expect(c.externalAuthorizedUserOptions.length).toBe(3);
      expect(c.applicantAttorneyOptions.map((o: any) => o.identityUserId)).toEqual(['u1']);
      expect(c.defenseAttorneyOptions.map((o: any) => o.identityUserId)).toEqual(['u2']);
    });
  });

  describe('claim information', () => {
    it('flattens the injury rows for the read-only table', () => {
      const c = create({
        rest: {
          'by-appointment': of([
            {
              appointmentInjuryDetail: {
                id: 'inj-1',
                dateOfInjury: '2026-01-05T00:00:00',
                isCumulativeInjury: true,
                claimNumber: 'WC-00042',
                wcabAdj: 'ADJ123',
                bodyPartsSummary: 'Left shoulder',
              },
              wcabOffice: { displayName: 'Van Nuys' },
              claimExaminer: { isActive: true, name: 'Grace Hopper' },
              primaryInsurance: { isActive: true, name: 'Statewide Mutual' },
              bodyParts: [
                { bodyPartDescription: ' Left shoulder ' },
                { bodyPartDescription: '  ' },
              ],
            },
          ]),
        },
      });

      c.loadInjuryDetails('appt-1');

      const detail = c.injuryDetails[0];
      expect(detail.id).toBe('inj-1');
      expect(detail.isCumulativeInjury).toBeTrue();
      expect(detail.wcabOfficeName).toBe('Van Nuys');
      expect(detail.claimExaminerName).toBe('Grace Hopper');
      expect(detail.insuranceCompanyName).toBe('Statewide Mutual');
    });

    it('drops blank body-part descriptions', () => {
      // A blank row would render as an empty bullet in the claim table.
      const c = create({
        rest: {
          'by-appointment': of([
            {
              appointmentInjuryDetail: { id: 'inj-1' },
              bodyParts: [{ bodyPartDescription: 'Neck' }, { bodyPartDescription: '   ' }, {}],
            },
          ]),
        },
      });

      c.loadInjuryDetails('appt-1');

      expect(c.injuryDetails[0].bodyParts).toEqual(['Neck']);
    });

    it('hides an INACTIVE claim examiner and insurer', () => {
      /**
       * A REMOVAL needing inactive rows seeded. A deactivated insurer or examiner must not
       * keep appearing against the claim -- with only active fixtures the guard is unreachable.
       */
      const c = create({
        rest: {
          'by-appointment': of([
            {
              appointmentInjuryDetail: { id: 'inj-1' },
              claimExaminer: { isActive: false, name: 'Retired Examiner' },
              primaryInsurance: { isActive: false, name: 'Former Insurer' },
            },
          ]),
        },
      });

      c.loadInjuryDetails('appt-1');

      expect(c.injuryDetails[0].claimExaminerName).toBe('');
      expect(c.injuryDetails[0].insuranceCompanyName).toBe('');
    });

    it('does nothing without an appointment id', () => {
      const c = create();
      restRequest.calls.reset();
      c.loadInjuryDetails(undefined);
      expect(restRequest).not.toHaveBeenCalled();
    });
  });

  describe('demographics download', () => {
    it('does nothing when no appointment is loaded', () => {
      const c = create({ roles: ['Staff Supervisor'] });
      c.downloadDemographics();
      expect(httpGet).not.toHaveBeenCalled();
    });

    it('requests the PDF as a blob for the loaded appointment', () => {
      const c = create({ roles: ['Staff Supervisor'] });
      withLoaded(c);
      httpGet.and.returnValue(of(new HttpResponse({ body: null })));

      c.downloadDemographics();

      expect(httpGet).toHaveBeenCalled();
      expect(httpGet.calls.mostRecent().args[0]).toContain('appointment-demographics/appt-1');
      expect(httpGet.calls.mostRecent().args[1].responseType).toBe('blob');
    });

    it('names the file from the content-disposition header', async () => {
      const c = create({ roles: ['Staff Supervisor'] });
      withLoaded(c);
      httpGet.and.returnValue(
        of(
          new HttpResponse({
            body: new Blob(['x'], { type: 'application/pdf' }),
            headers: new HttpHeaders({
              'content-disposition': 'attachment; filename="demographics-C0001.pdf"',
            }),
          }),
        ),
      );
      const click = spyOn(HTMLAnchorElement.prototype, 'click');
      spyOn(URL, 'createObjectURL').and.returnValue('blob:fake');
      spyOn(URL, 'revokeObjectURL');

      await c.downloadDemographicsInternal('appt-1');

      expect(click).toHaveBeenCalled();
    });

    it('reports a failed download rather than failing silently', async () => {
      // A blob download that does nothing is indistinguishable from a slow one.
      const c = create({ roles: ['Staff Supervisor'] });
      httpGet.and.returnValue(throwError(() => ({ status: 500 })));

      await c.downloadDemographicsInternal('appt-1');

      expect(c.errorMessage).toBe('Could not download the demographics PDF.');
    });

    it('does nothing when the response carries no body', async () => {
      const c = create({ roles: ['Staff Supervisor'] });
      httpGet.and.returnValue(of(new HttpResponse({ body: null })));
      const click = spyOn(HTMLAnchorElement.prototype, 'click');

      await c.downloadDemographicsInternal('appt-1');

      expect(click).not.toHaveBeenCalled();
      expect(c.errorMessage).toBe('');
    });
  });

  describe('small helpers', () => {
    it('reports a field invalid only once touched', () => {
      const c = create();
      expect(c.isFieldInvalid('patientFirstName')).toBeFalse();
      c.form.get('patientFirstName')?.markAsTouched();
      expect(c.isFieldInvalid('patientFirstName')).toBeTrue();
    });

    it('reports an unknown control as valid', () => {
      expect(create().isFieldInvalid('noSuchControl')).toBeFalse();
    });

    it('parses an ISO date of birth into a datepicker struct', () => {
      // ngbDatepicker needs NgbDateStruct and shows BLANK when handed a string, so this is
      // what makes the loaded DOB visible at all.
      const c = create();
      expect(c.parseDateOfBirthFromApi('1990-05-15T00:00:00')).toEqual({
        year: 1990,
        month: 5,
        day: 15,
      });
    });

    it('rejects an unusable date of birth rather than guessing', () => {
      const c = create();
      expect(c.parseDateOfBirthFromApi(null)).toBeNull();
      expect(c.parseDateOfBirthFromApi('')).toBeNull();
      expect(c.parseDateOfBirthFromApi('1990-05')).toBeNull();
      expect(c.parseDateOfBirthFromApi('not-a-date')).toBeNull();
    });

    it('formats a custom-field value for display', () => {
      const c = create();
      expect(typeof c.customFieldText({ fieldType: 1, value: 'x' })).toBe('string');
    });

    it('drops the gender sentinel from the radio options', () => {
      // Gender.Unspecified (0) has no localized label and renders the raw enum key.
      const c = create();
      expect(c.genderOptions.some((o: any) => o.value === 0)).toBeFalse();
    });

    it('navigates home from Back', () => {
      const c = create();
      c.goBack();
      expect(router.navigateByUrl).toHaveBeenCalledWith('/');
    });

    it('scrolls to the documents anchor when present', () => {
      /**
       * #805: this lookup silently returned null for the whole external audience, because only
       * the INTERNAL template carried the id -- the `?.` swallowed it and a button wired here
       * would have scrolled nowhere with no error.
       */
      const c = create();
      const anchor = document.createElement('div');
      anchor.id = 'appointment-documents-anchor';
      const scroll = spyOn(anchor, 'scrollIntoView');
      document.body.appendChild(anchor);

      try {
        c.openUploadDocuments();
        expect(scroll).toHaveBeenCalled();
      } finally {
        anchor.remove();
      }
    });

    it('does not throw when the documents anchor is absent', () => {
      const c = create();
      expect(() => c.openUploadDocuments()).not.toThrow();
    });
  });
});
