import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ConfigStateService, RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';

import { ClaimExaminerProfileComponent } from './claim-examiner-profile.component';

/**
 * The external claim-examiner "My profile" page -- the sibling of the attorney profile, for the
 * fourth external role.
 *
 * <p>It had NO spec and sat at 2 of 47 lines covered.</p>
 *
 * <p>The two components are near-twins in BEHAVIOUR and differ in PLUMBING, which is the reason
 * this is a separate spec rather than a parameterised one. The attorney page reads and writes
 * through the generated MyAttorneyProfile service; this one issues its GET and PUT through
 * RestService directly. Same contract, different collaborator, so the fixture is keyed on method
 * and URL instead of on a typed service.</p>
 *
 * <p>Same two deliberate omissions as the attorney spec, for the same reason: `save()` and
 * `loadStates()` subscribe with no `error` handler, so their failures are unhandled asynchronous
 * errors that no assertion can honestly catch -- a `not.toThrow()` there would pass while proving
 * nothing. Both are on the backlog. `loadProfile()` has an error handler and IS exercised.
 * `signOut()` is not called; it reaches the real OAuth stack.</p>
 *
 * <p>All names, addresses and identifiers below are synthetic.</p>
 */
describe('ClaimExaminerProfileComponent', () => {
  let rest: { request: jasmine.Spy };
  let toaster: { success: jasmine.Spy; error: jasmine.Spy };
  let router: { navigateByUrl: jasmine.Spy };
  let currentUser: Record<string, unknown> | null;
  let currentTenant: Record<string, unknown> | null;
  let profileResponse: unknown;
  let profileErrors: boolean;
  let lookupResponse: unknown;

  interface Probe {
    [key: string]: any;
  }

  const PROFILE = {
    email: 'examiner@example.test',
    concurrencyStamp: 'stamp-1',
    firstName: 'Grace',
    lastName: 'Hopper',
    phoneNumber: '555-0100',
    faxNumber: '555-0101',
    street: '1 Test Way',
    city: 'Encino',
    stateId: 'st-1',
    zipCode: '90000',
  };

  /** Requests to the state lookup and to the profile are told apart by URL. */
  function isLookup(req: { url?: string }): boolean {
    return (req.url ?? '').includes('state-lookup');
  }

  function create(): Probe {
    currentUser = {
      name: 'Grace',
      surname: 'Hopper',
      email: 'examiner@example.test',
      userName: 'grace',
      roles: ['Claim Examiner'],
    };
    currentTenant = { name: 'Example Practice' };
    profileResponse = PROFILE;
    profileErrors = false;
    lookupResponse = { items: [] };

    rest = {
      request: jasmine.createSpy('request').and.callFake((req: { url?: string }) => {
        if (isLookup(req)) {
          return of(lookupResponse);
        }
        return profileErrors ? throwError(() => ({ status: 404 })) : of(profileResponse);
      }),
    };
    toaster = { success: jasmine.createSpy('success'), error: jasmine.createSpy('error') };
    router = { navigateByUrl: jasmine.createSpy('navigateByUrl') };

    TestBed.configureTestingModule({
      providers: [
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

    return TestBed.createComponent(ClaimExaminerProfileComponent)
      .componentInstance as unknown as Probe;
  }

  type Req = { method?: string; url?: string; body?: Record<string, unknown> };

  /** The most recent non-lookup request, i.e. the profile GET or PUT. */
  function profileCall(): Req {
    const calls = (rest.request.calls.allArgs() as unknown[][])
      .map((a) => a[0] as Req)
      .filter((r) => !isLookup(r));
    return calls[calls.length - 1];
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('loading the profile', () => {
    it('fills the form from the stored record', () => {
      const c = create();
      c.ngOnInit();

      const v = c.form.getRawValue();
      expect(v.firstName).toBe('Grace');
      expect(v.lastName).toBe('Hopper');
      expect(v.city).toBe('Encino');
      expect(c.isLoading).toBeFalse();
    });

    it('reads the profile with a GET', () => {
      const c = create();
      c.ngOnInit();
      expect(profileCall().method).toBe('GET');
    });

    it('keeps the email off the form, where it is not editable', () => {
      const c = create();
      c.ngOnInit();
      expect(c.email).toBe('examiner@example.test');
      expect(c.form.getRawValue().email).toBeUndefined();
    });

    it('maps every absent field to null rather than undefined', () => {
      const c = create();
      profileResponse = {};
      c.ngOnInit();

      const v = c.form.getRawValue();
      expect(v.firstName).toBeNull();
      expect(v.stateId).toBeNull();
      expect(c.email).toBe('');
    });

    it('shows the not-found state when the record cannot be loaded', () => {
      const c = create();
      profileErrors = true;

      c.ngOnInit();

      expect(c.notFound).toBeTrue();
      expect(c.isLoading).withContext('finalize still runs').toBeFalse();
    });
  });

  describe('the state dropdown', () => {
    it('uses the lookup an external caller is allowed to call', () => {
      // The gated states CRUD endpoint 403s for an external party and trips the global
      // access overlay; this mirrors the attorney profile and the booking dropdown.
      const c = create();
      c.ngOnInit();

      const lookup = rest.request.calls
        .allArgs()
        .map((a) => a[0] as { url?: string })
        .find((r) => isLookup(r));
      expect(lookup?.url).toBe('/api/app/patients/state-lookup');
    });

    it('maps the lookup rows to id and name', () => {
      const c = create();
      lookupResponse = { items: [{ id: 'st-1', displayName: 'California' }] };
      c.ngOnInit();
      expect(c.states).toEqual([{ id: 'st-1', name: 'California' }]);
    });

    it('substitutes empty strings for a half-populated lookup row', () => {
      const c = create();
      lookupResponse = { items: [{}] };
      c.ngOnInit();
      expect(c.states).toEqual([{ id: '', name: '' }]);
    });

    it('leaves the list empty when the lookup returns no items', () => {
      const c = create();
      lookupResponse = {};
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
      expect(c.navDisplayName).toBe('Grace Hopper');

      currentUser = { userName: 'grace', email: 'examiner@example.test' };
      c.ngOnInit();
      expect(c.navDisplayName).toBe('grace');

      currentUser = { email: 'examiner@example.test' };
      c.ngOnInit();
      expect(c.navDisplayName).toBe('examiner@example.test');
    });

    it('shows nothing rather than undefined when the user has no name at all', () => {
      const c = create();
      currentUser = {};
      c.ngOnInit();
      expect(c.navDisplayName).toBe('');
      expect(c.navUserEmail).toBe('');
      expect(c.navRoleLabel).toBe('');
    });
  });

  describe('saving', () => {
    it('refuses an invalid form and shows the errors instead', () => {
      const c = create();
      c.ngOnInit();
      const before = rest.request.calls.count();
      c.form.patchValue({ firstName: 'a'.repeat(51) });

      c.save();

      expect(rest.request.calls.count()).withContext('nothing sent').toBe(before);
      expect(c.form.touched).toBeTrue();
    });

    it('writes the profile with a PUT carrying the loaded concurrency stamp', () => {
      const c = create();
      c.ngOnInit();

      c.save();

      const call = profileCall();
      expect(call.method).toBe('PUT');
      expect(call.body?.['firstName']).toBe('Grace');
      expect(call.body?.['concurrencyStamp']).toBe('stamp-1');
    });

    it('adopts the NEW stamp from the response, so a second save still works', () => {
      // Keeping the old stamp makes the first save succeed and every one after it fail.
      const c = create();
      c.ngOnInit();
      profileResponse = { ...PROFILE, concurrencyStamp: 'stamp-2' };

      c.save();
      c.save();

      expect(profileCall().body?.['concurrencyStamp']).toBe('stamp-2');
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
