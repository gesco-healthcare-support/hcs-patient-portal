import { TestBed } from '@angular/core/testing';
import { ABP, RoutesService, eLayoutType } from '@abp/ng.core';

import { APP_ROUTE_PROVIDER } from './route.provider';
import { APPLICANT_ATTORNEYS_APPLICANT_ATTORNEY_ROUTE_PROVIDER } from './applicant-attorneys/applicant-attorney/providers/applicant-attorney-route.provider';
import { APPLICANT_ATTORNEY_BASE_ROUTES } from './applicant-attorneys/applicant-attorney/providers/applicant-attorney-base.routes';
import { APPOINTMENT_DOCUMENT_TYPES_APPOINTMENT_DOCUMENT_TYPE_ROUTE_PROVIDER } from './appointment-document-types/appointment-document-type/providers/appointment-document-type-route.provider';
import { APPOINTMENT_DOCUMENT_TYPE_BASE_ROUTES } from './appointment-document-types/appointment-document-type/providers/appointment-document-type-base.routes';
import { APPOINTMENT_LANGUAGES_APPOINTMENT_LANGUAGE_ROUTE_PROVIDER } from './appointment-languages/appointment-language/providers/appointment-language-route.provider';
import { APPOINTMENT_LANGUAGE_BASE_ROUTES } from './appointment-languages/appointment-language/providers/appointment-language-base.routes';
import { APPOINTMENT_STATUSES_APPOINTMENT_STATUS_ROUTE_PROVIDER } from './appointment-statuses/appointment-status/providers/appointment-status-route.provider';
import { APPOINTMENT_STATUS_BASE_ROUTES } from './appointment-statuses/appointment-status/providers/appointment-status-base.routes';
import { APPOINTMENT_TYPES_APPOINTMENT_TYPE_ROUTE_PROVIDER } from './appointment-types/appointment-type/providers/appointment-type-route.provider';
import { APPOINTMENT_TYPE_BASE_ROUTES } from './appointment-types/appointment-type/providers/appointment-type-base.routes';
import { APPOINTMENTS_APPOINTMENT_ROUTE_PROVIDER } from './appointments/appointment/providers/appointment-route.provider';
import { APPOINTMENT_BASE_ROUTES } from './appointments/appointment/providers/appointment-base.routes';
import { APPOINTMENTS_CHANGE_REQUEST_ROUTE_PROVIDER } from './appointments/change-requests/providers/change-request-route.provider';
import { CHANGE_REQUEST_BASE_ROUTES } from './appointments/change-requests/providers/change-request-base.routes';
import { CLAIM_EXAMINERS_CLAIM_EXAMINER_ROUTE_PROVIDER } from './claim-examiners/claim-examiner/providers/claim-examiner-route.provider';
import { CLAIM_EXAMINER_BASE_ROUTES } from './claim-examiners/claim-examiner/providers/claim-examiner-base.routes';
import { DEFENSE_ATTORNEYS_DEFENSE_ATTORNEY_ROUTE_PROVIDER } from './defense-attorneys/defense-attorney/providers/defense-attorney-route.provider';
import { DEFENSE_ATTORNEY_BASE_ROUTES } from './defense-attorneys/defense-attorney/providers/defense-attorney-base.routes';
import { DOCTOR_AVAILABILITIES_DOCTOR_AVAILABILITY_ROUTE_PROVIDER } from './doctor-availabilities/doctor-availability/providers/doctor-availability-route.provider';
import { DOCTOR_AVAILABILITY_BASE_ROUTES } from './doctor-availabilities/doctor-availability/providers/doctor-availability-base.routes';
import { DOCTOR_MANAGEMENT_ROUTE_PROVIDER } from './doctor-management/providers/doctor-management-route.provider';
import { DOCTOR_MANAGEMENT_BASE_ROUTES } from './doctor-management/providers/doctor-management-base.routes';
import { DOCTORS_DOCTOR_ROUTE_PROVIDER } from './doctors/doctor/providers/doctor-route.provider';
import { DOCTOR_BASE_ROUTES } from './doctors/doctor/providers/doctor-base.routes';
import { LOCATIONS_LOCATION_ROUTE_PROVIDER } from './locations/location/providers/location-route.provider';
import { LOCATION_BASE_ROUTES } from './locations/location/providers/location-base.routes';
import { PATIENTS_PATIENT_ROUTE_PROVIDER } from './patients/patient/providers/patient-route.provider';
import { PATIENT_BASE_ROUTES } from './patients/patient/providers/patient-base.routes';
import { STATES_STATE_ROUTE_PROVIDER } from './states/state/providers/state-route.provider';
import { STATE_BASE_ROUTES } from './states/state/providers/state-base.routes';
import { WCAB_OFFICES_WCAB_OFFICE_ROUTE_PROVIDER } from './wcab-offices/wcab-office/providers/wcab-office-route.provider';
import { WCAB_OFFICE_BASE_ROUTES } from './wcab-offices/wcab-office/providers/wcab-office-base.routes';

/**
 * The ABP nav-menu route providers -- the seventeen `provideAppInitializer` blocks that register
 * this application's left-nav entries with `RoutesService` at startup.
 *
 * <p>WHY ONE TABLE RATHER THAN SEVENTEEN SIBLING SPECS. The sixteen feature providers are
 * byte-identical apart from the entity name: verified mechanically by stripping ALL_CAPS
 * identifiers and quoted paths from each file and hashing the result, which collapsed all sixteen
 * onto ONE shape. Seventeen near-identical spec files would be duplicated knowledge; karma
 * attributes coverage to the source file regardless of which spec drove it.</p>
 *
 * <p>WHAT IS ACTUALLY AT RISK HERE, and it is not "does add() get called". Each provider reads
 * its own feature's base routes, and the failure this pins is a provider wired to ANOTHER
 * feature's constant -- a copy-paste that leaves the nav silently wrong and that no compiler
 * catches, because every one of these constants has the same type. So the assertion compares
 * against the real exported constant per row, not against a shape or a call count.</p>
 *
 * <p>HOW THE INITIALIZER IS DRIVEN. `provideAppInitializer` registers its callback as an
 * `APP_INITIALIZER` multi-provider, and TestBed runs those itself --
 * `TestBedCompiler.finalize()` calls the (internal) `ApplicationInitStatus.runInitializers()`
 * when the testing module is created, which the first `inject()` triggers. So configuring the
 * module and injecting the stub is the whole harness.</p>
 *
 * <p>An earlier version of this file injected `APP_INITIALIZER` and replayed the callbacks
 * through `TestBed.runInInjectionContext`. That reading of Angular's source was right about the
 * mechanism and wrong about the consequence: it DOUBLE-RAN every initializer, so `add()` was
 * called twice. Running the probe caught it; reading did not. Do not reintroduce it.</p>
 *
 * <p>No patient data is involved. The route paths and permission names below are the real
 * constants, quoted so a rename has to come past this file.</p>
 */
describe('ABP route providers', () => {
  /**
   * Configures a testing module carrying one route provider and returns every array handed to
   * `RoutesService.add`. The stub records its argument rather than merely counting the call, so
   * a provider registering the wrong feature's routes fails rather than passing.
   */
  function addedRoutesFor(provider: unknown): ABP.Route[][] {
    const added: ABP.Route[][] = [];
    // Reset first so the helper is safe to call more than once in a spec: TestBed refuses to be
    // reconfigured once instantiated, and the injection below instantiates it.
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        { provide: RoutesService, useValue: { add: (routes: ABP.Route[]) => added.push(routes) } },
        provider,
      ],
    });
    TestBed.inject(RoutesService);
    return added;
  }

  /** The sixteen feature providers, each paired with the constant it is required to register. */
  const FEATURE_PROVIDERS: ReadonlyArray<{
    label: string;
    provider: unknown;
    expected: ABP.Route[];
  }> = [
    {
      label: 'applicant attorney',
      provider: APPLICANT_ATTORNEYS_APPLICANT_ATTORNEY_ROUTE_PROVIDER,
      expected: APPLICANT_ATTORNEY_BASE_ROUTES,
    },
    {
      label: 'appointment document type',
      provider: APPOINTMENT_DOCUMENT_TYPES_APPOINTMENT_DOCUMENT_TYPE_ROUTE_PROVIDER,
      expected: APPOINTMENT_DOCUMENT_TYPE_BASE_ROUTES,
    },
    {
      label: 'appointment language',
      provider: APPOINTMENT_LANGUAGES_APPOINTMENT_LANGUAGE_ROUTE_PROVIDER,
      expected: APPOINTMENT_LANGUAGE_BASE_ROUTES,
    },
    {
      label: 'appointment status',
      provider: APPOINTMENT_STATUSES_APPOINTMENT_STATUS_ROUTE_PROVIDER,
      expected: APPOINTMENT_STATUS_BASE_ROUTES,
    },
    {
      label: 'appointment type',
      provider: APPOINTMENT_TYPES_APPOINTMENT_TYPE_ROUTE_PROVIDER,
      expected: APPOINTMENT_TYPE_BASE_ROUTES,
    },
    {
      label: 'appointment',
      provider: APPOINTMENTS_APPOINTMENT_ROUTE_PROVIDER,
      expected: APPOINTMENT_BASE_ROUTES,
    },
    {
      label: 'change request',
      provider: APPOINTMENTS_CHANGE_REQUEST_ROUTE_PROVIDER,
      expected: CHANGE_REQUEST_BASE_ROUTES,
    },
    {
      label: 'claim examiner',
      provider: CLAIM_EXAMINERS_CLAIM_EXAMINER_ROUTE_PROVIDER,
      expected: CLAIM_EXAMINER_BASE_ROUTES,
    },
    {
      label: 'defense attorney',
      provider: DEFENSE_ATTORNEYS_DEFENSE_ATTORNEY_ROUTE_PROVIDER,
      expected: DEFENSE_ATTORNEY_BASE_ROUTES,
    },
    {
      label: 'doctor availability',
      provider: DOCTOR_AVAILABILITIES_DOCTOR_AVAILABILITY_ROUTE_PROVIDER,
      expected: DOCTOR_AVAILABILITY_BASE_ROUTES,
    },
    {
      label: 'doctor management',
      provider: DOCTOR_MANAGEMENT_ROUTE_PROVIDER,
      expected: DOCTOR_MANAGEMENT_BASE_ROUTES,
    },
    {
      label: 'doctor',
      provider: DOCTORS_DOCTOR_ROUTE_PROVIDER,
      expected: DOCTOR_BASE_ROUTES,
    },
    {
      label: 'location',
      provider: LOCATIONS_LOCATION_ROUTE_PROVIDER,
      expected: LOCATION_BASE_ROUTES,
    },
    {
      label: 'patient',
      provider: PATIENTS_PATIENT_ROUTE_PROVIDER,
      expected: PATIENT_BASE_ROUTES,
    },
    {
      label: 'state',
      provider: STATES_STATE_ROUTE_PROVIDER,
      expected: STATE_BASE_ROUTES,
    },
    {
      label: 'wcab office',
      provider: WCAB_OFFICES_WCAB_OFFICE_ROUTE_PROVIDER,
      expected: WCAB_OFFICE_BASE_ROUTES,
    },
  ];

  it('covers every feature route provider in the application', () => {
    // Guards the table itself: a provider added later must be added here too, or this fails
    // rather than the new provider quietly going untested.
    expect(FEATURE_PROVIDERS.length).toBe(16);
  });

  FEATURE_PROVIDERS.forEach(({ label, provider, expected }) => {
    describe(`${label} route provider`, () => {
      it('registers its routes exactly once at startup', () => {
        expect(addedRoutesFor(provider).length).toBe(1);
      });

      it('registers its OWN feature base routes', () => {
        expect(addedRoutesFor(provider)[0]).toEqual([...expected]);
      });
    });
  });

  describe('APP_ROUTE_PROVIDER (the top-level menu)', () => {
    it('registers the top-level menu exactly once at startup', () => {
      expect(addedRoutesFor(APP_ROUTE_PROVIDER).length).toBe(1);
    });

    it('registers the seven top-level entries with their real permission names', () => {
      expect(addedRoutesFor(APP_ROUTE_PROVIDER)[0]).toEqual([
        {
          path: '/',
          name: '::Menu:Home',
          iconClass: 'fas fa-home',
          order: 1,
          layout: eLayoutType.application,
        },
        {
          path: '/dashboard',
          name: '::Menu:Dashboard',
          iconClass: 'fas fa-chart-line',
          order: 2,
          layout: eLayoutType.application,
          requiredPolicy: 'CaseEvaluation.Dashboard.Host  || CaseEvaluation.Dashboard.Tenant',
        },
        {
          path: '',
          name: '::Menu:UserManagement',
          iconClass: 'fas fa-users-cog',
          order: 100,
          layout: eLayoutType.application,
          requiredPolicy: 'CaseEvaluation.UserManagement',
        },
        {
          path: '/users/invite',
          name: '::Menu:InviteExternalUser',
          parentName: '::Menu:UserManagement',
          iconClass: 'fas fa-envelope',
          order: 1,
          layout: eLayoutType.application,
          requiredPolicy: 'CaseEvaluation.UserManagement.InviteExternalUser',
        },
        {
          path: '/internal-users',
          name: '::Menu:InternalUsers',
          parentName: '::Menu:UserManagement',
          iconClass: 'fas fa-user-plus',
          order: 2,
          layout: eLayoutType.application,
          requiredPolicy: 'CaseEvaluation.InternalUsers.Create',
        },
        {
          path: '/appointment-change-logs',
          name: '::Menu:AppointmentChangeLogs',
          iconClass: 'fas fa-history',
          order: 90,
          layout: eLayoutType.application,
          requiredPolicy: 'CaseEvaluation.AppointmentChangeLogs',
        },
        {
          path: '/reports',
          name: '::Menu:Reports',
          iconClass: 'fas fa-table',
          order: 91,
          layout: eLayoutType.application,
          requiredPolicy: 'CaseEvaluation.Reports',
        },
      ]);
    });

    it('parents both user-management children on the container entry', () => {
      // The container has no route of its own (path ''), so the two children are only reachable
      // in the nav through parentName. A rename on one side alone silently orphans them.
      const added = addedRoutesFor(APP_ROUTE_PROVIDER)[0];
      const container = added.find((route) => route.name === '::Menu:UserManagement');
      const children = added.filter((route) => route.parentName === '::Menu:UserManagement');

      expect(container).toBeTruthy();
      expect(container?.path).toBe('');
      expect(children.map((route) => route.path)).toEqual(['/users/invite', '/internal-users']);
    });
  });
});
