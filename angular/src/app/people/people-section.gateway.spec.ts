import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { PeopleSectionGateway } from './people-section.gateway';
import { PatientService } from '../proxy/patients/patient.service';
import { ApplicantAttorneyService } from '../proxy/applicant-attorneys/applicant-attorney.service';
import { DefenseAttorneyService } from '../proxy/defense-attorneys/defense-attorney.service';
import { ClaimExaminerService } from '../proxy/claim-examiners/claim-examiner.service';
import { ExternalSignupService } from '../proxy/external-signups/external-signup.service';
import { AppointmentService } from '../proxy/appointments/appointment.service';
import type { PersonFormState } from './people.util';

/**
 * The People gateway: the layer that maps one People section onto the right generated proxy.
 *
 * <p>It sat at 2 of 52 covered lines, and unlike the rest of this tranche it is not a component
 * -- there is no template, no change detection and no DI beyond six proxies, so it needs no
 * component harness at all. That makes it the cheapest coverage here and the most direct to
 * assert: every method is a pure mapping with a real rule inside it.</p>
 *
 * <p>The rules worth pinning are the ones a reader would not guess. Attorney DTOs carry no
 * email of their own in some shapes, so the row falls back to the linked identity. The SSN is
 * write-only -- blank means "leave the stored value alone", which is the opposite of what
 * sending an empty string would do. And every text field trims to null rather than to '', so a
 * cleared field is stored as absent rather than as empty.</p>
 *
 * <p>All names, emails and addresses below are synthetic.</p>
 */
describe('PeopleSectionGateway', () => {
  let patients: Record<string, jasmine.Spy>;
  let aa: Record<string, jasmine.Spy>;
  let da: Record<string, jasmine.Spy>;
  let ce: Record<string, jasmine.Spy>;
  let externalSignup: Record<string, jasmine.Spy>;
  let appointments: Record<string, jasmine.Spy>;
  let gateway: PeopleSectionGateway;

  function crud(): Record<string, jasmine.Spy> {
    return {
      getList: jasmine.createSpy('getList').and.returnValue(of({ items: [] })),
      create: jasmine.createSpy('create').and.returnValue(of({})),
      update: jasmine.createSpy('update').and.returnValue(of({})),
      delete: jasmine.createSpy('delete').and.returnValue(of(undefined)),
    };
  }

  /** A fully-populated form; each test overrides only the fields it is about. */
  function form(over: Partial<PersonFormState> = {}): PersonFormState {
    return {
      id: 'person-1',
      identityUserId: 'identity-1',
      firstName: 'Ada',
      lastName: 'Lovelace',
      email: 'ada@example.test',
      phoneNumber: '5555550100',
      middleName: 'Byron',
      genderId: 2,
      dateOfBirth: '1980-01-01',
      cellPhoneNumber: '5555550101',
      phoneNumberTypeId: 1,
      socialSecurityNumber: '',
      street: '1 Market Street',
      city: 'Encino',
      stateId: 'state-1',
      zipCode: '90001',
      appointmentLanguageId: 'lang-1',
      othersLanguageName: '',
      interpreterVendorName: '',
      apptNumber: 'Unit 4',
      firmName: 'Lovelace LLP',
      firmAddress: '2 Market Street',
      webAddress: 'https://example.test',
      faxNumber: '5555550102',
      concurrencyStamp: 'stamp-1',
      ...over,
    };
  }

  beforeEach(() => {
    patients = {
      ...crud(),
      getStateLookup: jasmine.createSpy('getStateLookup').and.returnValue(of({ items: [] })),
      getAppointmentLanguageLookup: jasmine
        .createSpy('getAppointmentLanguageLookup')
        .and.returnValue(of({ items: [] })),
    };
    aa = crud();
    da = crud();
    ce = crud();
    externalSignup = {
      getActiveInvitedEmails: jasmine.createSpy('getActiveInvitedEmails').and.returnValue(of([])),
    };
    appointments = {
      getList: jasmine.createSpy('getList').and.returnValue(of({ items: [] })),
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: PatientService, useValue: patients },
        { provide: ApplicantAttorneyService, useValue: aa },
        { provide: DefenseAttorneyService, useValue: da },
        { provide: ClaimExaminerService, useValue: ce },
        { provide: ExternalSignupService, useValue: externalSignup },
        { provide: AppointmentService, useValue: appointments },
      ],
    });
    gateway = TestBed.inject(PeopleSectionGateway);
  });

  afterEach(() => TestBed.resetTestingModule());

  function firstRow(section: 'patients' | 'aa' | 'da' | 'ce') {
    let row: any;
    gateway.list(section).subscribe((rows) => (row = rows[0]));
    return row;
  }

  describe('listing patients', () => {
    it('flattens the nav-properties response into a row', () => {
      patients['getList'].and.returnValue(
        of({
          items: [
            {
              patient: {
                id: 'p1',
                firstName: 'Ada',
                lastName: 'Lovelace',
                email: 'ada@example.test',
                phoneNumber: '5555550100',
                identityUserId: 'identity-1',
                concurrencyStamp: 'stamp-1',
              },
              state: { name: 'California' },
              appointmentLanguage: { name: 'Spanish' },
            },
          ],
        }),
      );

      const row = firstRow('patients');

      expect(row.id).toBe('p1');
      expect(row.firstName).toBe('Ada');
      expect(row.stateName).toBe('California');
      expect(row.languageName).toBe('Spanish');
      expect(row.concurrencyStamp).toBe('stamp-1');
    });

    it('marks a patient with a portal account as linked', () => {
      patients['getList'].and.returnValue(
        of({ items: [{ patient: { id: 'p1', identityUserId: 'identity-1' } }] }),
      );
      expect(firstRow('patients').portal).toBe('linked');
    });

    it('marks a record-only patient as none', () => {
      // The component upgrades a non-linked row to "invited" after the invitation
      // lookup resolves; the gateway deliberately reports only what it can see.
      patients['getList'].and.returnValue(of({ items: [{ patient: { id: 'p1' } }] }));
      expect(firstRow('patients').portal).toBe('none');
    });

    it('substitutes empty strings and nulls for absent fields', () => {
      patients['getList'].and.returnValue(of({ items: [{ patient: {} }] }));
      const row = firstRow('patients');
      expect(row.id).toBe('');
      expect(row.firstName).toBe('');
      expect(row.email).toBeNull();
      expect(row.stateName).toBeNull();
    });

    it('returns an empty list when the page has no items', () => {
      patients['getList'].and.returnValue(of({}));
      let rows: unknown[] = [];
      gateway.list('patients').subscribe((r) => (rows = r));
      expect(rows).toEqual([]);
    });
  });

  describe('listing attorneys', () => {
    it('uses the applicant service for the applicant section', () => {
      gateway.list('aa').subscribe();
      expect(aa['getList']).toHaveBeenCalled();
      expect(da['getList']).not.toHaveBeenCalled();
    });

    it('uses the defense service for the defense section', () => {
      gateway.list('da').subscribe();
      expect(da['getList']).toHaveBeenCalled();
      expect(aa['getList']).not.toHaveBeenCalled();
    });

    it('prefers the attorney record own email', () => {
      aa['getList'].and.returnValue(
        of({
          items: [
            {
              applicantAttorney: { id: 'a1', email: 'firm@example.test' },
              identityUser: { email: 'login@example.test' },
            },
          ],
        }),
      );
      expect(firstRow('aa').email).toBe('firm@example.test');
    });

    it('falls back to the linked identity email', () => {
      /**
       * A REMOVAL-shaped guarantee in reverse: the fallback only matters when the
       * attorney DTO has NO email, so the fixture omits it. Some attorney shapes carry
       * no email field at all, and without the fallback those rows would show blank
       * against a person who does have a login.
       */
      aa['getList'].and.returnValue(
        of({
          items: [
            { applicantAttorney: { id: 'a1' }, identityUser: { email: 'login@example.test' } },
          ],
        }),
      );
      expect(firstRow('aa').email).toBe('login@example.test');
    });

    it('reports null when neither carries an email', () => {
      aa['getList'].and.returnValue(of({ items: [{ applicantAttorney: { id: 'a1' } }] }));
      expect(firstRow('aa').email).toBeNull();
    });

    it('carries the firm fields onto the row', () => {
      da['getList'].and.returnValue(
        of({
          items: [
            {
              defenseAttorney: {
                id: 'd1',
                firmName: 'Turing & Co',
                firmAddress: '2 Market Street',
                webAddress: 'https://example.test',
                identityUserId: 'identity-9',
              },
              state: { name: 'California' },
            },
          ],
        }),
      );

      const row = firstRow('da');

      expect(row.firmName).toBe('Turing & Co');
      expect(row.webAddress).toBe('https://example.test');
      expect(row.stateName).toBe('California');
      expect(row.portal).toBe('linked');
    });
  });

  describe('listing claim examiners', () => {
    it('flattens the examiner row', () => {
      ce['getList'].and.returnValue(
        of({
          items: [
            {
              claimExaminer: {
                id: 'c1',
                firstName: 'Grace',
                lastName: 'Hopper',
                email: 'grace@example.test',
                faxNumber: '5555550102',
                identityUserId: 'identity-9',
              },
              state: { name: 'California' },
            },
          ],
        }),
      );

      const row = firstRow('ce');

      expect(row.id).toBe('c1');
      expect(row.faxNumber).toBe('5555550102');
      expect(row.portal).toBe('linked');
      expect(row.stateName).toBe('California');
    });
  });

  describe('lookups and side queries', () => {
    it('asks the signup service which emails have an open invitation', () => {
      externalSignup['getActiveInvitedEmails'].and.returnValue(of(['ada@example.test']));
      let result: string[] = [];
      gateway
        .activeInvitedEmails(['ada@example.test', 'grace@example.test'])
        .subscribe((r) => (result = r));
      expect(externalSignup['getActiveInvitedEmails']).toHaveBeenCalledWith([
        'ada@example.test',
        'grace@example.test',
      ]);
      expect(result).toEqual(['ada@example.test']);
    });

    it('filters appointments to the one patient', () => {
      appointments['getList'].and.returnValue(of({ items: [{ appointment: { id: 'appt-1' } }] }));
      let rows: unknown[] = [];
      gateway.appointmentsForPatient('p1').subscribe((r) => (rows = r));
      expect(appointments['getList'].calls.mostRecent().args[0].patientId).toBe('p1');
      expect(rows.length).toBe(1);
    });

    it('returns an empty appointment list rather than undefined', () => {
      appointments['getList'].and.returnValue(of({}));
      let rows: unknown[] = [];
      gateway.appointmentsForPatient('p1').subscribe((r) => (rows = r));
      expect(rows).toEqual([]);
    });

    it('reshapes the state lookup into id and name options', () => {
      patients['getStateLookup'].and.returnValue(
        of({ items: [{ id: 'state-1', displayName: 'California' }] }),
      );
      let options: unknown[] = [];
      gateway.stateLookup().subscribe((o) => (options = o));
      expect(options).toEqual([{ id: 'state-1', name: 'California' }]);
    });

    it('reshapes the language lookup the same way', () => {
      patients['getAppointmentLanguageLookup'].and.returnValue(
        of({ items: [{ id: 'lang-1', displayName: 'Spanish' }] }),
      );
      let options: unknown[] = [];
      gateway.languageLookup().subscribe((o) => (options = o));
      expect(options).toEqual([{ id: 'lang-1', name: 'Spanish' }]);
    });

    it('tolerates a lookup with no items', () => {
      patients['getStateLookup'].and.returnValue(of({}));
      let options: unknown[] = [];
      gateway.stateLookup().subscribe((o) => (options = o));
      expect(options).toEqual([]);
    });
  });

  describe('creating', () => {
    it('routes each section to its own service', () => {
      gateway.create('patients', form()).subscribe();
      expect(patients['create']).toHaveBeenCalled();
      gateway.create('aa', form()).subscribe();
      expect(aa['create']).toHaveBeenCalled();
      gateway.create('da', form()).subscribe();
      expect(da['create']).toHaveBeenCalled();
      gateway.create('ce', form()).subscribe();
      expect(ce['create']).toHaveBeenCalled();
    });

    it('trims the patient text fields', () => {
      gateway.create('patients', form({ firstName: '  Ada  ', city: '  Encino  ' })).subscribe();
      const body = patients['create'].calls.mostRecent().args[0];
      expect(body.firstName).toBe('Ada');
      expect(body.city).toBe('Encino');
    });

    it('stores a cleared text field as null rather than an empty string', () => {
      // An empty string is a VALUE; null is absence. Sending '' would overwrite a
      // stored address with a blank one rather than clearing it.
      gateway.create('patients', form({ city: '   ', middleName: '' })).subscribe();
      const body = patients['create'].calls.mostRecent().args[0];
      expect(body.city).toBeNull();
      expect(body.middleName).toBeNull();
    });

    it('omits the SSN entirely when it is blank', () => {
      /**
       * Write-only field: blank means "leave the stored SSN unchanged", which the backend
       * implements by preserving on empty. Sending '' would be a different instruction.
       * The default fixture leaves it blank, so this is the ordinary case.
       */
      gateway.create('patients', form()).subscribe();
      expect(patients['create'].calls.mostRecent().args[0].socialSecurityNumber).toBeUndefined();
    });

    it('sends the SSN when one was typed', () => {
      gateway
        .create('patients', form({ socialSecurityNumber: '  SYNTHETIC-PLACEHOLDER  ' }))
        .subscribe();
      expect(patients['create'].calls.mostRecent().args[0].socialSecurityNumber).toBe(
        'SYNTHETIC-PLACEHOLDER',
      );
    });

    it('omits an unset date of birth', () => {
      gateway.create('patients', form({ dateOfBirth: '' })).subscribe();
      expect(patients['create'].calls.mostRecent().args[0].dateOfBirth).toBeUndefined();
    });

    it('preserves the identity link on the payload', () => {
      // The modal never edits this; it carries the row's value straight through so an
      // update cannot silently unlink a portal account.
      gateway.create('patients', form({ identityUserId: 'identity-9' })).subscribe();
      expect(patients['create'].calls.mostRecent().args[0].identityUserId).toBe('identity-9');
    });

    it('sends the attorney firm fields', () => {
      gateway.create('aa', form()).subscribe();
      const body = aa['create'].calls.mostRecent().args[0];
      expect(body.firmName).toBe('Lovelace LLP');
      expect(body.webAddress).toBe('https://example.test');
      expect(body.stateId).toBe('state-1');
    });

    it('does not send patient-only fields on an attorney', () => {
      gateway.create('aa', form()).subscribe();
      const body = aa['create'].calls.mostRecent().args[0];
      expect(body.socialSecurityNumber).toBeUndefined();
      expect(body.dateOfBirth).toBeUndefined();
    });

    it('sends only the examiner fields for a claim examiner', () => {
      gateway.create('ce', form()).subscribe();
      const body = ce['create'].calls.mostRecent().args[0];
      expect(body.firstName).toBe('Ada');
      expect(body.faxNumber).toBe('5555550102');
      expect(body.firmName).toBeUndefined();
    });

    it('stores a cleared state as null', () => {
      gateway.create('ce', form({ stateId: '' })).subscribe();
      expect(ce['create'].calls.mostRecent().args[0].stateId).toBeNull();
    });
  });

  describe('updating', () => {
    it('routes each section to its own service with the row id', () => {
      gateway.update('patients', form({ id: 'p1' })).subscribe();
      expect(patients['update'].calls.mostRecent().args[0]).toBe('p1');
      gateway.update('aa', form({ id: 'a1' })).subscribe();
      expect(aa['update'].calls.mostRecent().args[0]).toBe('a1');
      gateway.update('da', form({ id: 'd1' })).subscribe();
      expect(da['update'].calls.mostRecent().args[0]).toBe('d1');
      gateway.update('ce', form({ id: 'c1' })).subscribe();
      expect(ce['update'].calls.mostRecent().args[0]).toBe('c1');
    });

    it('carries the concurrency stamp on every section', () => {
      /**
       * The stamp is what makes the update concurrency-checked. Dropping it would let two
       * operators silently overwrite each other's edit to the same person -- and these
       * records carry patient contact details.
       */
      for (const [section, service] of [
        ['patients', patients],
        ['aa', aa],
        ['da', da],
        ['ce', ce],
      ] as Array<['patients' | 'aa' | 'da' | 'ce', Record<string, jasmine.Spy>]>) {
        gateway.update(section, form({ concurrencyStamp: 'stamp-9' })).subscribe();
        expect(service['update'].calls.mostRecent().args[1].concurrencyStamp)
          .withContext(section)
          .toBe('stamp-9');
      }
    });

    it('applies the same trimming rules as create', () => {
      gateway.update('patients', form({ firstName: '  Ada  ', city: '  ' })).subscribe();
      const body = patients['update'].calls.mostRecent().args[1];
      expect(body.firstName).toBe('Ada');
      expect(body.city).toBeNull();
    });

    it('still omits a blank SSN on update', () => {
      // The riskiest place for this rule: an edit that does not retype the SSN must not
      // wipe the stored one.
      gateway.update('patients', form({ socialSecurityNumber: '' })).subscribe();
      expect(patients['update'].calls.mostRecent().args[1].socialSecurityNumber).toBeUndefined();
    });
  });

  describe('deleting', () => {
    it('routes each section to its own service', () => {
      gateway.delete('patients', 'p1').subscribe();
      expect(patients['delete']).toHaveBeenCalledWith('p1');
      gateway.delete('aa', 'a1').subscribe();
      expect(aa['delete']).toHaveBeenCalledWith('a1');
      gateway.delete('da', 'd1').subscribe();
      expect(da['delete']).toHaveBeenCalledWith('d1');
      gateway.delete('ce', 'c1').subscribe();
      expect(ce['delete']).toHaveBeenCalledWith('c1');
    });
  });
});
