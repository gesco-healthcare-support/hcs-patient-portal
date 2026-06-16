import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { PatientService } from '../proxy/patients/patient.service';
import { ApplicantAttorneyService } from '../proxy/applicant-attorneys/applicant-attorney.service';
import { DefenseAttorneyService } from '../proxy/defense-attorneys/defense-attorney.service';
import { ClaimExaminerService } from '../proxy/claim-examiners/claim-examiner.service';
import { ExternalSignupService } from '../proxy/external-signups/external-signup.service';
import { AppointmentService } from '../proxy/appointments/appointment.service';
import type { AppointmentWithNavigationPropertiesDto } from '../proxy/appointments/models';
import type { ApplicantAttorneyDto } from '../proxy/applicant-attorneys/models';
import type { DefenseAttorneyDto } from '../proxy/defense-attorneys/models';
import type { IdentityUserDto } from '../proxy/volo/abp/identity/models';
import type { StateDto } from '../proxy/states/models';
import type { Gender } from '../proxy/enums/gender.enum';
import type { PhoneNumberType } from '../proxy/enums/phone-number-type.enum';
import type { PersonFormState, PersonRow, PeopleSectionKey } from './people.util';

const PAGE = { maxResultCount: 500, skipCount: 0 };

/** Minimal lookup option for the modal's State / Language selects. */
export interface LookupOption {
  id: string;
  name: string;
}

/**
 * Maps a People section to the right generated proxy so the hub component stays
 * free of per-section branching. List rows are normalized to {@link PersonRow}
 * with a preliminary portal status (linked when an IdentityUser is attached,
 * else none); the component upgrades non-linked rows to "invited" after
 * {@link activeInvitedEmails} resolves. Attorney DTOs carry no email field, so
 * their create/update omit it.
 */
@Injectable({ providedIn: 'root' })
export class PeopleSectionGateway {
  private readonly patients = inject(PatientService);
  private readonly aa = inject(ApplicantAttorneyService);
  private readonly da = inject(DefenseAttorneyService);
  private readonly ce = inject(ClaimExaminerService);
  private readonly externalSignup = inject(ExternalSignupService);
  private readonly appointments = inject(AppointmentService);

  list(section: PeopleSectionKey): Observable<PersonRow[]> {
    switch (section) {
      case 'patients':
        return this.patients.getList(PAGE).pipe(
          map((r) =>
            (r.items ?? []).map((n) => {
              const p = n.patient;
              return {
                id: p?.id ?? '',
                firstName: p?.firstName ?? '',
                lastName: p?.lastName ?? '',
                email: p?.email ?? null,
                phoneNumber: p?.phoneNumber ?? null,
                identityUserId: p?.identityUserId ?? null,
                portal: p?.identityUserId ? 'linked' : 'none',
                middleName: p?.middleName ?? null,
                genderId: p?.genderId ?? null,
                dateOfBirth: p?.dateOfBirth ?? null,
                cellPhoneNumber: p?.cellPhoneNumber ?? null,
                phoneNumberTypeId: p?.phoneNumberTypeId ?? null,
                socialSecurityNumber: p?.socialSecurityNumber ?? null,
                apptNumber: p?.apptNumber ?? null,
                street: p?.street ?? null,
                city: p?.city ?? null,
                stateId: p?.stateId ?? null,
                stateName: n.state?.name ?? null,
                zipCode: p?.zipCode ?? null,
                appointmentLanguageId: p?.appointmentLanguageId ?? null,
                languageName: n.appointmentLanguage?.name ?? null,
                othersLanguageName: p?.othersLanguageName ?? null,
                interpreterVendorName: p?.interpreterVendorName ?? null,
                concurrencyStamp: p?.concurrencyStamp,
              } as PersonRow;
            }),
          ),
        );
      case 'aa':
        return this.aa
          .getList(PAGE)
          .pipe(
            map((r) =>
              (r.items ?? []).map((n) =>
                this.attorneyRow(n.applicantAttorney, n.identityUser, n.state),
              ),
            ),
          );
      case 'da':
        return this.da
          .getList(PAGE)
          .pipe(
            map((r) =>
              (r.items ?? []).map((n) =>
                this.attorneyRow(n.defenseAttorney, n.identityUser, n.state),
              ),
            ),
          );
      case 'ce':
        return this.ce.getList(PAGE).pipe(
          map((r) =>
            (r.items ?? []).map((n) => {
              const c = n.claimExaminer;
              return {
                id: c?.id ?? '',
                firstName: c?.firstName ?? '',
                lastName: c?.lastName ?? '',
                email: c?.email ?? null,
                phoneNumber: c?.phoneNumber ?? null,
                faxNumber: c?.faxNumber ?? null,
                identityUserId: c?.identityUserId ?? null,
                portal: c?.identityUserId ? 'linked' : 'none',
                street: c?.street ?? null,
                city: c?.city ?? null,
                stateId: c?.stateId ?? null,
                stateName: n.state?.name ?? null,
                zipCode: c?.zipCode ?? null,
                concurrencyStamp: c?.concurrencyStamp,
              } as PersonRow;
            }),
          ),
        );
    }
  }

  // Applicant + Defense attorneys share an identical row shape. Email is read
  // from the linked IdentityUser (the attorney DTO has no email field).
  private attorneyRow(
    a: ApplicantAttorneyDto | DefenseAttorneyDto | undefined,
    identityUser: IdentityUserDto | null | undefined,
    state: StateDto | null | undefined,
  ): PersonRow {
    return {
      id: a?.id ?? '',
      firstName: a?.firstName ?? '',
      lastName: a?.lastName ?? '',
      email: identityUser?.email ?? null,
      phoneNumber: a?.phoneNumber ?? null,
      identityUserId: a?.identityUserId ?? null,
      portal: a?.identityUserId ? 'linked' : 'none',
      firmName: a?.firmName ?? null,
      firmAddress: a?.firmAddress ?? null,
      webAddress: a?.webAddress ?? null,
      faxNumber: a?.faxNumber ?? null,
      street: a?.street ?? null,
      city: a?.city ?? null,
      stateId: a?.stateId ?? null,
      stateName: state?.name ?? null,
      zipCode: a?.zipCode ?? null,
      concurrencyStamp: a?.concurrencyStamp,
    } as PersonRow;
  }

  /** Subset of emails with an active invitation (B3). Caller gates on permission. */
  activeInvitedEmails(emails: string[]): Observable<string[]> {
    return this.externalSignup.getActiveInvitedEmails(emails);
  }

  /** One patient's appointments for the detail table (B5 patientId filter). */
  appointmentsForPatient(patientId: string): Observable<AppointmentWithNavigationPropertiesDto[]> {
    return this.appointments
      .getList({ patientId, maxResultCount: 100, skipCount: 0 })
      .pipe(map((r) => r.items ?? []));
  }

  stateLookup(): Observable<LookupOption[]> {
    return this.patients
      .getStateLookup({ maxResultCount: 100, skipCount: 0, filter: '' })
      .pipe(
        map((r) => (r.items ?? []).map((s) => ({ id: s.id ?? '', name: s.displayName ?? '' }))),
      );
  }

  languageLookup(): Observable<LookupOption[]> {
    return this.patients
      .getAppointmentLanguageLookup({ maxResultCount: 100, skipCount: 0, filter: '' })
      .pipe(
        map((r) => (r.items ?? []).map((l) => ({ id: l.id ?? '', name: l.displayName ?? '' }))),
      );
  }

  create(section: PeopleSectionKey, form: PersonFormState): Observable<unknown> {
    switch (section) {
      case 'patients':
        return this.patients.create(this.patientBody(form));
      case 'aa':
        return this.aa.create(this.attorneyBody(form));
      case 'da':
        return this.da.create(this.attorneyBody(form));
      case 'ce':
        return this.ce.create(this.examinerBody(form));
    }
  }

  update(section: PeopleSectionKey, form: PersonFormState): Observable<unknown> {
    const id = form.id as string;
    const stamp = form.concurrencyStamp;
    switch (section) {
      case 'patients':
        return this.patients.update(id, { ...this.patientBody(form), concurrencyStamp: stamp });
      case 'aa':
        return this.aa.update(id, { ...this.attorneyBody(form), concurrencyStamp: stamp });
      case 'da':
        return this.da.update(id, { ...this.attorneyBody(form), concurrencyStamp: stamp });
      case 'ce':
        return this.ce.update(id, { ...this.examinerBody(form), concurrencyStamp: stamp });
    }
  }

  delete(section: PeopleSectionKey, id: string): Observable<void> {
    switch (section) {
      case 'patients':
        return this.patients.delete(id);
      case 'aa':
        return this.aa.delete(id);
      case 'da':
        return this.da.delete(id);
      case 'ce':
        return this.ce.delete(id);
    }
  }

  // ---- form -> DTO bodies ----
  private patientBody(f: PersonFormState) {
    const ssn = f.socialSecurityNumber.trim();
    return {
      firstName: f.firstName.trim(),
      lastName: f.lastName.trim(),
      middleName: f.middleName.trim() || null,
      email: f.email.trim(),
      identityUserId: f.identityUserId,
      genderId: (f.genderId ?? undefined) as Gender | undefined,
      dateOfBirth: f.dateOfBirth || undefined,
      phoneNumber: f.phoneNumber.trim() || null,
      phoneNumberTypeId: (f.phoneNumberTypeId ?? undefined) as PhoneNumberType | undefined,
      cellPhoneNumber: f.cellPhoneNumber.trim() || null,
      // Write-only: blank leaves the stored SSN unchanged (backend preserves on empty).
      socialSecurityNumber: ssn || undefined,
      street: f.street.trim() || null,
      city: f.city.trim() || null,
      stateId: f.stateId || null,
      zipCode: f.zipCode.trim() || null,
      appointmentLanguageId: f.appointmentLanguageId || null,
      othersLanguageName: f.othersLanguageName.trim() || null,
      interpreterVendorName: f.interpreterVendorName.trim() || null,
      apptNumber: f.apptNumber.trim() || null,
    };
  }

  private attorneyBody(f: PersonFormState) {
    return {
      identityUserId: f.identityUserId,
      firstName: f.firstName.trim() || null,
      lastName: f.lastName.trim() || null,
      firmName: f.firmName.trim() || null,
      firmAddress: f.firmAddress.trim() || null,
      webAddress: f.webAddress.trim() || null,
      phoneNumber: f.phoneNumber.trim() || null,
      faxNumber: f.faxNumber.trim() || null,
      stateId: f.stateId || null,
    };
  }

  private examinerBody(f: PersonFormState) {
    return {
      identityUserId: f.identityUserId,
      firstName: f.firstName.trim() || null,
      lastName: f.lastName.trim() || null,
      email: f.email.trim() || null,
      phoneNumber: f.phoneNumber.trim() || null,
      faxNumber: f.faxNumber.trim() || null,
      stateId: f.stateId || null,
    };
  }
}
