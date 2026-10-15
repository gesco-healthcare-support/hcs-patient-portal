import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ConfigStateService, RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';

import { PatientProfileRedesignComponent } from './patient-profile-redesign.component';

/**
 * My Profile (redesign) -- EXTENDS PatientProfileComponent to inherit the whole profile engine,
 * adding the four per-section-editable cards and their shared save-confirm modal.
 *
 * <p>It sat at 71 of 96 lines uncovered. An existing spec (sweep #645) covers the Escape handler,
 * `initials` and `avatarColor`; none of those is repeated here.</p>
 *
 * <p>`super.ngOnInit()` is deliberately not driven, for the same reason as the internal detail:
 * it pulls the inherited two-load topology, and the base has an unguarded subscribe in that
 * chain. The form and the loaded patient are seeded directly.</p>
 *
 * <p>TWO METHODS ARE DELIBERATELY NOT CALLED:</p>
 * <ul>
 *   <li>`changePassword()` assigns `window.location.href`. Location is [Unforgeable], so it
 *       cannot be stubbed, and the assignment performs a REAL navigation that takes the karma
 *       page with it -- the same trap that disconnected the browser at 549 of 682 in an earlier
 *       tranche. Its one line is left uncovered on purpose.</li>
 *   <li>`onLogout()` delegates to the real full-logout helper, which reaches the OAuth stack.</li>
 * </ul>
 *
 * <p>All names, emails and dates below are synthetic.</p>
 */
describe('PatientProfileRedesignComponent surfaces', () => {
  let rest: { request: jasmine.Spy };
  let toaster: { success: jasmine.Spy; error: jasmine.Spy };
  let router: { navigateByUrl: jasmine.Spy };
  let currentUser: Record<string, unknown> | null;

  interface Probe {
    [key: string]: any;
  }

  function create(): Probe {
    currentUser = { roles: ['Patient'], userName: 'ada', name: 'Ada', surname: 'Lovelace' };

    rest = {
      request: jasmine.createSpy('request').and.returnValue(of({ items: [], totalCount: 0 })),
    };
    toaster = { success: jasmine.createSpy('success'), error: jasmine.createSpy('error') };
    router = { navigateByUrl: jasmine.createSpy('navigateByUrl') };

    TestBed.configureTestingModule({
      providers: [
        { provide: RestService, useValue: rest },
        {
          provide: ConfigStateService,
          useValue: {
            getOne: (key: string) => (key === 'currentUser' ? currentUser : { name: 'Office' }),
            getDeep: () => null,
            getDeep$: () => of(null),
            getOne$: () => of(null),
            getAll$: () => of({}),
          },
        },
        { provide: Router, useValue: router },
        { provide: ToasterService, useValue: toaster },
      ],
    });

    return TestBed.createComponent(PatientProfileRedesignComponent)
      .componentInstance as unknown as Probe;
  }

  /** Shadow an inherited getter for the duration of one test. */
  function shadow(c: Probe, name: string, value: unknown): void {
    Object.defineProperty(c, name, { value, configurable: true });
  }

  /** Drop the inherited validators so confirmSave reaches its PUT. */
  function makeValid(c: Probe): void {
    type Ctrl = { clearValidators(): void; updateValueAndValidity(options: unknown): void };
    (Object.values(c.form.controls) as Ctrl[]).forEach((ctrl) => {
      ctrl.clearValidators();
      ctrl.updateValueAndValidity({ emitEvent: false });
    });
    c.form.updateValueAndValidity({ emitEvent: false });
  }

  /** Seed the loaded patient the save path writes back into. */
  function loaded(c: Probe): void {
    c.selected = { patient: { id: 'p-1', concurrencyStamp: 'stamp-1' } };
  }

  afterEach(() => TestBed.resetTestingModule());

  describe('who is looking at the page', () => {
    it('treats a non-external-non-patient caller as the patient', () => {
      const c = create();
      shadow(c, 'isExternalUserNonPatient', false);
      expect(c.isPatient).toBeTrue();
    });

    it('treats an attorney or examiner as not the patient', () => {
      const c = create();
      shadow(c, 'isExternalUserNonPatient', true);
      expect(c.isPatient).toBeFalse();
    });

    it('titles the hero with the patient name for a patient', () => {
      const c = create();
      shadow(c, 'isExternalUserNonPatient', false);
      c.form.patchValue({ firstName: 'Ada', lastName: 'Lovelace' });
      expect(c.profileDisplayName).toBe('Ada Lovelace');
    });

    it('titles it with the firm-aware name for everyone else', () => {
      const c = create();
      shadow(c, 'isExternalUserNonPatient', true);
      c.firmName = 'Analytical Engines LLP';
      expect(c.profileDisplayName).toBe('Ada Lovelace');

      currentUser = { roles: ['Applicant Attorney'], userName: 'aa' };
      expect(c.profileDisplayName).toBe('Analytical Engines LLP');
    });

    it('falls back to the display user name when the form carries no name', () => {
      const c = create();
      shadow(c, 'isExternalUserNonPatient', false);
      shadow(c, 'displayUserName', 'ada');
      c.form.patchValue({ firstName: '', lastName: '' });
      expect(c.patientName).toBe('ada');
    });

    it('prefers the signed-in email over the form value', () => {
      // The config-state email is the account's; the form's is the patient record's, and
      // they can differ.
      const c = create();
      currentUser = { email: 'ada@example.test' };
      c.form.patchValue({ email: 'other@example.test' });
      expect(c.userEmail).toBe('ada@example.test');
    });

    it('falls back to the form email when the account carries none', () => {
      const c = create();
      currentUser = {};
      c.form.patchValue({ email: 'other@example.test' });
      expect(c.userEmail).toBe('other@example.test');
    });

    it('labels the role from the inherited display name', () => {
      const c = create();
      shadow(c, 'displayRoleName', 'Applicant Attorney');
      expect(c.roleLabel).toBe('Applicant Attorney');
    });
  });

  describe('the read ledger', () => {
    it('renders a form value as a display string', () => {
      const c = create();
      c.form.patchValue({ firstName: 'Ada' });
      expect(c.fv('firstName')).toBe('Ada');
      expect(c.fv('not-a-control')).toBe('');
    });

    it('treats null and empty alike as nothing to show', () => {
      const c = create();
      c.form.patchValue({ firstName: null });
      expect(c.fv('firstName')).toBe('');
    });

    it('shows only the date part of a stored timestamp', () => {
      // The native date input needs YYYY-MM-DD; the loaded value carries a time.
      const c = create();
      c.form.patchValue({ dateOfBirth: '1980-01-01T00:00:00Z' });
      expect(c.dobDisplay()).toBe('1980-01-01');
    });

    it('names the gender and the phone type from the inherited option lists', () => {
      const c = create();
      shadow(c, 'genderOptions', [{ key: 'Female', value: 2 }]);
      shadow(c, 'phoneNumberTypeOptions', [{ key: 'Mobile', value: 1 }]);
      c.form.patchValue({ genderId: 2, phoneNumberTypeId: 1 });

      expect(c.genderLabel()).toBe('Female');
      expect(c.phoneTypeLabel()).toBe('Mobile');
    });

    it('shows nothing for a value outside those lists', () => {
      const c = create();
      shadow(c, 'genderOptions', []);
      shadow(c, 'phoneNumberTypeOptions', []);
      c.form.patchValue({ genderId: 9999, phoneNumberTypeId: 9999 });

      expect(c.genderLabel()).toBe('');
      expect(c.phoneTypeLabel()).toBe('');
    });

    it('resolves a state and a language to their labels', () => {
      const c = create();
      c.states = [{ id: 'st-1', label: 'California' }];
      c.languages = [{ id: 'lang-1', label: 'Spanish' }];
      c.form.patchValue({ stateId: 'st-1', appointmentLanguageId: 'lang-1' });

      expect(c.stateLabel()).toBe('California');
      expect(c.languageLabel()).toBe('Spanish');
    });

    it('falls back to the raw id when the lookup has not loaded', () => {
      // Better to show the stored value than a blank row that looks like missing data.
      const c = create();
      c.states = [];
      c.languages = [];
      c.form.patchValue({ stateId: 'st-1', appointmentLanguageId: 'lang-1' });

      expect(c.stateLabel()).toBe('st-1');
      expect(c.languageLabel()).toBe('lang-1');
    });

    it('summarises the interpreter preference as the vendor or a plain no', () => {
      const c = create();
      c.form.patchValue({ interpreterVendorName: 'Vendor A' });
      expect(c.interpreterSummary()).toContain('Vendor A');

      c.form.patchValue({ interpreterVendorName: '' });
      expect(c.interpreterSummary()).toBe('No');
    });
  });

  describe('editing a section', () => {
    it('snapshots the form so cancel reverts the whole section', () => {
      const c = create();
      c.form.patchValue({ city: 'Encino' });

      c.startEdit('address');
      c.form.patchValue({ city: 'Edited' });
      c.cancelEdit();

      expect(c.form.get('city').value).toBe('Encino');
      expect(c.editingSection).toBeNull();
    });

    it('trims the stored timestamp when the personal section opens', () => {
      /**
       * A native date input shows nothing at all for a value carrying a time component,
       * so the picker would open blank over a date the patient already has.
       */
      const c = create();
      c.form.patchValue({ dateOfBirth: '1980-01-01T00:00:00Z' });

      c.startEdit('personal');

      expect(c.form.get('dateOfBirth').value).toBe('1980-01-01');
      expect(c.editingSection).toBe('personal');
    });

    it('leaves an already-trimmed date alone', () => {
      const c = create();
      c.form.patchValue({ dateOfBirth: '1980-01-01' });
      c.startEdit('personal');
      expect(c.form.get('dateOfBirth').value).toBe('1980-01-01');
    });

    it('derives the interpreter toggle when the preferences section opens', () => {
      // There is no backend boolean; the toggle is inferred from the vendor name.
      const c = create();
      c.form.patchValue({ interpreterVendorName: 'Vendor A' });
      c.startEdit('preferences');
      expect(c.needsInterpreter).toBeTrue();

      c.form.patchValue({ interpreterVendorName: '' });
      c.startEdit('preferences');
      expect(c.needsInterpreter).toBeFalse();
    });

    it('opens and closes the confirmation', () => {
      const c = create();
      c.requestSave();
      expect(c.confirmVisible).toBeTrue();

      c.cancelConfirm();
      expect(c.confirmVisible).toBeFalse();
    });
  });

  describe('saving', () => {
    it('refuses an invalid form, shows the errors and closes the confirmation', () => {
      const c = create();
      loaded(c);
      c.form.get('email').setValidators(() => ({ required: true }));
      c.form.get('email').updateValueAndValidity();
      c.confirmVisible = true;

      c.confirmSave();

      expect(rest.request).not.toHaveBeenCalled();
      expect(c.form.touched).toBeTrue();
      expect(c.confirmVisible).toBeFalse();
    });

    it('refuses when no patient record is loaded', () => {
      const c = create();
      makeValid(c);
      c.selected = null;
      c.confirmVisible = true;

      c.confirmSave();

      expect(rest.request).not.toHaveBeenCalled();
      expect(c.confirmVisible).toBeFalse();
    });

    it('puts the whole form to the me endpoint, carrying the concurrency stamp', () => {
      // One PUT persists everything; the non-edited sections carry their loaded values.
      const c = create();
      makeValid(c);
      loaded(c);
      rest.request.and.returnValue(of({ id: 'p-1' }));

      c.confirmSave();

      const [req] = rest.request.calls.mostRecent().args;
      expect(req.method).toBe('PUT');
      expect(req.url).toBe('/api/app/patients/me');
      expect(req.body.concurrencyStamp).toBe('stamp-1');
    });

    it('CLEARS the interpreter vendor when the toggle is turned off', () => {
      /**
       * The vendor name IS the stored state of the toggle, so turning it off has to blank
       * the name. Leaving it would persist a vendor for a patient who just said they do
       * not need one, and the next load would switch the toggle back on.
       */
      const c = create();
      makeValid(c);
      loaded(c);
      c.form.patchValue({ interpreterVendorName: 'Vendor A' });
      c.editingSection = 'preferences';
      c.needsInterpreter = false;
      rest.request.and.returnValue(of({ id: 'p-1' }));

      c.confirmSave();

      expect(rest.request.calls.mostRecent().args[0].body.interpreterVendorName).toBeNull();
    });

    it('keeps the vendor when the toggle is still on', () => {
      const c = create();
      makeValid(c);
      loaded(c);
      c.form.patchValue({ interpreterVendorName: 'Vendor A' });
      c.editingSection = 'preferences';
      c.needsInterpreter = true;
      rest.request.and.returnValue(of({ id: 'p-1' }));

      c.confirmSave();

      expect(rest.request.calls.mostRecent().args[0].body.interpreterVendorName).toBe('Vendor A');
    });

    it('does not blank the vendor while editing some other section', () => {
      const c = create();
      makeValid(c);
      loaded(c);
      c.form.patchValue({ interpreterVendorName: 'Vendor A' });
      c.editingSection = 'address';
      c.needsInterpreter = false;
      rest.request.and.returnValue(of({ id: 'p-1' }));

      c.confirmSave();

      expect(rest.request.calls.mostRecent().args[0].body.interpreterVendorName).toBe('Vendor A');
    });

    it('sends an absent date of birth as undefined rather than null', () => {
      const c = create();
      makeValid(c);
      loaded(c);
      c.form.patchValue({ dateOfBirth: null });
      rest.request.and.returnValue(of({ id: 'p-1' }));

      c.confirmSave();

      expect(rest.request.calls.mostRecent().args[0].body.dateOfBirth).toBeUndefined();
    });

    it('merges the server response back into the loaded record', () => {
      // The response carries the new concurrency stamp; without the merge the next save
      // would send a stale one and 409.
      const c = create();
      makeValid(c);
      loaded(c);
      rest.request.and.returnValue(of({ id: 'p-1', concurrencyStamp: 'stamp-2' }));

      c.confirmSave();

      expect(c.selected.patient.concurrencyStamp).toBe('stamp-2');
    });

    it('closes the editor and the confirmation, and reports success', () => {
      const c = create();
      makeValid(c);
      loaded(c);
      c.editingSection = 'personal';
      c.confirmVisible = true;
      rest.request.and.returnValue(of({ id: 'p-1' }));

      c.confirmSave();

      expect(c.editingSection).toBeNull();
      expect(c.confirmVisible).toBeFalse();
      expect(toaster.success).toHaveBeenCalledWith('Profile changes saved.');
      expect(c.isBusy).toBeFalse();
    });

    it('STAYS in the section when the save fails, so the edits survive', () => {
      const c = create();
      makeValid(c);
      loaded(c);
      c.editingSection = 'personal';
      c.confirmVisible = true;
      rest.request.and.returnValue(throwError(() => ({ status: 409 })));

      c.confirmSave();

      expect(c.editingSection)
        .withContext('dropping out of edit mode would hide the unsaved work')
        .toBe('personal');
      expect(c.confirmVisible).toBeFalse();
      expect(c.isBusy).toBeFalse();
    });
  });

  describe('the lookups and the firm name', () => {
    it('maps the state and language lookups', () => {
      const c = create();
      rest.request.and.returnValue(
        of({ items: [{ id: 'st-1', displayName: 'California' }], totalCount: 1 }),
      );

      c.loadLookups();

      expect(c.states).toEqual([{ id: 'st-1', label: 'California' }]);
      expect(c.languages).toEqual([{ id: 'st-1', label: 'California' }]);
    });

    it('drops a lookup row with no id rather than offering a blank option', () => {
      const c = create();
      rest.request.and.returnValue(of({ items: [{ displayName: 'Orphan' }], totalCount: 1 }));
      c.loadLookups();
      expect(c.states).toEqual([]);
    });

    it('substitutes an empty label for a row with no display name', () => {
      const c = create();
      rest.request.and.returnValue(of({ items: [{ id: 'st-1' }], totalCount: 1 }));
      c.loadLookups();
      expect(c.states).toEqual([{ id: 'st-1', label: '' }]);
    });

    it('leaves the lookups empty when they fail', () => {
      const c = create();
      rest.request.and.returnValue(throwError(() => ({ status: 500 })));
      c.loadLookups();
      expect(c.states).toEqual([]);
      expect(c.languages).toEqual([]);
    });

    it('stores the firm name, and blanks it when the lookup fails', () => {
      const c = create();
      rest.request.and.returnValue(of({ firmName: 'Analytical Engines LLP' }));
      c.loadFirmName();
      expect(c.firmName).toBe('Analytical Engines LLP');

      rest.request.and.returnValue(of({}));
      c.loadFirmName();
      expect(c.firmName).toBe('');

      rest.request.and.returnValue(throwError(() => ({ status: 404 })));
      expect(() => c.loadFirmName()).not.toThrow();
    });
  });

  describe('navigation', () => {
    it('returns home from both the documents and the back entries', () => {
      const c = create();
      c.onDocuments();
      expect(router.navigateByUrl).toHaveBeenCalledWith('/');

      router.navigateByUrl.calls.reset();
      c.backHome();
      expect(router.navigateByUrl).toHaveBeenCalledWith('/');
    });
  });
});
