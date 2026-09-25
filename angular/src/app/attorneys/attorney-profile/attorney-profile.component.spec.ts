import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ConfigStateService, RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';

import { AttorneyProfileComponent } from './attorney-profile.component';
import { MyAttorneyProfileService } from '../../proxy/my-attorney-profiles/my-attorney-profile.service';

/**
 * The external attorney "My profile" page. An applicant or defense attorney edits their own
 * master record through the self-scoped MyAttorneyProfile endpoint, which resolves the caller
 * from CurrentUser.Id.
 *
 * <p>It had NO spec and sat at 1 of 49 lines covered.</p>
 *
 * <p>The contract worth stating is what a save here does NOT do: it updates the attorney master
 * record only, and never rewrites past appointments, which keep their booking-time snapshot.
 * Nothing in this component could break that -- the endpoint decides it -- so what IS pinned is
 * the part this component owns: the concurrency stamp round-trip, which is what stops a second
 * save from colliding with the first.</p>
 *
 * <p>TWO PATHS ARE DELIBERATELY NOT TESTED. `save()` and `loadStates()` both subscribe with a
 * `next` handler and NO `error` handler, so a failure there is an unhandled RxJS error. Asserting
 * anything about it would reproduce the vacuous shape this session hit in tranche 5: the error is
 * asynchronous, so nothing throws synchronously for an assertion to catch, and the test passes
 * while proving nothing. Both are logged to the backlog instead. `loadProfile()` DOES have an
 * error handler, so its failure is exercised below.</p>
 *
 * <p>`signOut()` is not called either -- it delegates to the real full-logout helper, which
 * reaches the OAuth stack.</p>
 *
 * <p>All names, firms, addresses and identifiers below are synthetic.</p>
 */
describe('AttorneyProfileComponent', () => {
  let api: Record<string, jasmine.Spy>;
  let rest: { request: jasmine.Spy };
  let toaster: { success: jasmine.Spy; error: jasmine.Spy };
  let router: { navigateByUrl: jasmine.Spy };
  let currentUser: Record<string, unknown> | null;
  let currentTenant: Record<string, unknown> | null;

  interface Probe {
    [key: string]: any;
  }

  const PROFILE = {
    kind: 'applicant',
    email: 'ada@example.test',
    concurrencyStamp: 'stamp-1',
    firstName: 'Ada',
    lastName: 'Lovelace',
    firmName: 'Analytical Engines LLP',
    webAddress: 'https://engines.example.test',
    phoneNumber: '555-0100',
    faxNumber: '555-0101',
    street: '1 Test Way',
    city: 'Encino',
    stateId: 'st-1',
    zipCode: '90000',
  };

  function create(): Probe {
    currentUser = {
      name: 'Ada',
      surname: 'Lovelace',
      email: 'ada@example.test',
      userName: 'ada',
      roles: ['Applicant Attorney'],
    };
    currentTenant = { name: 'Example Practice' };

    api = {
      get: jasmine.createSpy('get').and.returnValue(of(PROFILE)),
      update: jasmine
        .createSpy('update')
        .and.returnValue(of({ ...PROFILE, concurrencyStamp: 'stamp-2' })),
    };
    rest = { request: jasmine.createSpy('request').and.returnValue(of({ items: [] })) };
    toaster = { success: jasmine.createSpy('success'), error: jasmine.createSpy('error') };
    router = { navigateByUrl: jasmine.createSpy('navigateByUrl') };

    TestBed.configureTestingModule({
      providers: [
        { provide: MyAttorneyProfileService, useValue: api },
        { provide: RestService, useValue: rest },
        { provide: ToasterService, useValue: toaster },
        { provide: Router, useValue: router },
        {
          provide: ConfigStateService,
          useValue: {
            getOne: (k: string) => (k === 'currentUser' ? currentUser : currentTenant),
          },
        },
      ],
    });

    return TestBed.createComponent(AttorneyProfileComponent).componentInstance as unknown as Probe;
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('loading the profile', () => {
    it('fills the form from the stored record', () => {
      const c = create();
      c.ngOnInit();

      const v = c.form.getRawValue();
      expect(v.firstName).toBe('Ada');
      expect(v.firmName).toBe('Analytical Engines LLP');
      expect(v.city).toBe('Encino');
      expect(v.zipCode).toBe('90000');
      expect(c.isLoading).toBeFalse();
    });

    it('keeps the email and the kind off the form, where they are not editable', () => {
      // Both are displayed but never sent back; putting them in the form would make
      // them look editable and would widen the update payload.
      const c = create();
      c.ngOnInit();
      expect(c.email).toBe('ada@example.test');
      expect(c.kind).toBe('applicant');
      expect(c.form.getRawValue().email).toBeUndefined();
    });

    it('maps every absent field to null rather than undefined', () => {
      const c = create();
      api['get'].and.returnValue(of({ kind: 'applicant' }));
      c.ngOnInit();

      const v = c.form.getRawValue();
      expect(v.firstName).toBeNull();
      expect(v.firmName).toBeNull();
      expect(v.stateId).toBeNull();
    });

    it('treats a missing kind and email as empty strings', () => {
      const c = create();
      api['get'].and.returnValue(of({}));
      c.ngOnInit();
      expect(c.kind).toBe('');
      expect(c.email).toBe('');
    });

    it('shows the not-found state when the record cannot be loaded', () => {
      // This path HAS an error handler, which is why it is exercised.
      const c = create();
      api['get'].and.returnValue(throwError(() => ({ status: 404 })));

      c.ngOnInit();

      expect(c.notFound).toBeTrue();
      expect(c.isLoading).withContext('finalize still runs').toBeFalse();
    });
  });

  describe('the role label', () => {
    it('names a defense attorney', () => {
      const c = create();
      api['get'].and.returnValue(of({ ...PROFILE, kind: 'defense' }));
      c.ngOnInit();
      expect(c.roleLabel).toBe('Defense Attorney');
    });

    it('names an applicant attorney, and defaults to it for anything else', () => {
      const c = create();
      c.ngOnInit();
      expect(c.roleLabel).toBe('Applicant Attorney');

      c.kind = '';
      expect(c.roleLabel).toBe('Applicant Attorney');
    });
  });

  describe('the state dropdown', () => {
    it('uses the lookup an external caller is allowed to call', () => {
      /**
       * The gated /api/app/states CRUD endpoint 403s for an external attorney and trips
       * the global access overlay. The patient state-lookup is the un-gated equivalent,
       * and this is what keeps the two from being swapped back.
       */
      const c = create();
      c.ngOnInit();

      const [req] = rest.request.calls.mostRecent().args;
      expect(req.method).toBe('GET');
      expect(req.url).toBe('/api/app/patients/state-lookup');
    });

    it('maps the lookup rows to id and name', () => {
      const c = create();
      rest.request.and.returnValue(of({ items: [{ id: 'st-1', displayName: 'California' }] }));
      c.ngOnInit();
      expect(c.states).toEqual([{ id: 'st-1', name: 'California' }]);
    });

    it('substitutes empty strings for a half-populated lookup row', () => {
      const c = create();
      rest.request.and.returnValue(of({ items: [{}] }));
      c.ngOnInit();
      expect(c.states).toEqual([{ id: '', name: '' }]);
    });

    it('leaves the list empty when the lookup returns no items', () => {
      const c = create();
      rest.request.and.returnValue(of({}));
      c.ngOnInit();
      expect(c.states).toEqual([]);
    });
  });

  describe('the navbar', () => {
    it('names the office from the tenant, falling back to a generic title', () => {
      const c = create();
      c.ngOnInit();
      expect(c.navClinicName).toBe('Example Practice');

      currentTenant = null;
      c.ngOnInit();
      expect(c.navClinicName).toBe('Appointment Portal');
    });

    it('prefers the full name, then the user name, then the email', () => {
      const c = create();
      c.ngOnInit();
      expect(c.navDisplayName).toBe('Ada Lovelace');

      currentUser = { userName: 'ada', email: 'ada@example.test' };
      c.ngOnInit();
      expect(c.navDisplayName).toBe('ada');

      currentUser = { email: 'ada@example.test' };
      c.ngOnInit();
      expect(c.navDisplayName).toBe('ada@example.test');
    });

    it('shows nothing rather than undefined when the user has no name at all', () => {
      const c = create();
      currentUser = {};
      c.ngOnInit();
      expect(c.navDisplayName).toBe('');
      expect(c.navUserEmail).toBe('');
      expect(c.navRoleLabel).toBe('');
    });

    it('takes the first role as the label', () => {
      const c = create();
      currentUser = { roles: ['Applicant Attorney', 'Something Else'] };
      c.ngOnInit();
      expect(c.navRoleLabel).toBe('Applicant Attorney');
    });
  });

  describe('saving', () => {
    it('refuses an invalid form and shows the errors instead', () => {
      const c = create();
      c.ngOnInit();
      c.form.patchValue({ firstName: 'a'.repeat(51) });

      c.save();

      expect(api['update']).not.toHaveBeenCalled();
      expect(c.form.touched).toBeTrue();
    });

    it('sends the whole form with the loaded concurrency stamp', () => {
      // Without the stamp the update is a lost-update waiting to happen; the endpoint
      // rejects it, and the user sees a failure they cannot act on.
      const c = create();
      c.ngOnInit();

      c.save();

      expect(api['update']).toHaveBeenCalled();
      const body = api['update'].calls.mostRecent().args[0];
      expect(body.firstName).toBe('Ada');
      expect(body.concurrencyStamp).toBe('stamp-1');
    });

    it('adopts the NEW stamp from the response, so a second save still works', () => {
      /**
       * The response carries the next stamp. Keeping the old one would make the first
       * save succeed and every save after it fail, which reads as an intermittent bug.
       */
      const c = create();
      c.ngOnInit();

      c.save();
      c.save();

      expect(api['update'].calls.mostRecent().args[0].concurrencyStamp).toBe('stamp-2');
    });

    it('confirms the save and releases the button', () => {
      const c = create();
      c.ngOnInit();
      c.save();
      expect(toaster.success).toHaveBeenCalledWith('Profile changes saved.');
      expect(c.isBusy).toBeFalse();
    });

    it('caps each field at its stored length', () => {
      const c = create();
      c.ngOnInit();
      const tooLong: Record<string, number> = {
        firstName: 51,
        lastName: 51,
        firmName: 256,
        webAddress: 101,
        phoneNumber: 21,
        faxNumber: 21,
        street: 256,
        city: 51,
        zipCode: 16,
      };
      Object.entries(tooLong).forEach(([field, len]) => {
        c.form.get(field).setValue('a'.repeat(len));
        expect(c.form.get(field).hasError('maxlength')).withContext(field).toBeTrue();
        c.form.get(field).setValue('ok');
      });
    });
  });

  describe('navigation', () => {
    it('goes back to the external home', () => {
      const c = create();
      c.backHome();
      expect(router.navigateByUrl).toHaveBeenCalledWith('/');
    });
  });
});
