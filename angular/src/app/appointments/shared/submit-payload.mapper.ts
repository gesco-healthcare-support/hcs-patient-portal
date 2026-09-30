import type {
  AppointmentInjurySubmitDto,
  AppointmentSubmitDto,
} from '../../proxy/appointments/models';
import type { PatientWithNavigationPropertiesDto } from '../../proxy/patients/models';
import { formatDateOfBirthForApi } from '../../shared/date-of-birth.util';
import { unitToDto } from './patient-unit.mapper';
import type { AppointmentInjuryDraft } from '../sections/appointment-add-claim-information.component';
import type { AppointmentAuthorizedUserDraft } from '../sections/appointment-add-authorized-users.component';

/**
 * The booking wizard's submit payload, as pure functions (#603, item B PR2 T10).
 *
 * <p>These were ten private methods on `AppointmentAddComponent`. Nothing about them needed
 * to be there: each is a projection of the form's raw value plus, at most, a couple of
 * scalars. Living on a 3,900-line template-less `@Directive()` with eleven injected services
 * made the most consequential mapping in the app -- the one that decides what a booking
 * actually sends -- effectively untestable, so none of it was tested.</p>
 *
 * <p>Extraction is verbatim. Every guard, every `?? undefined`, every field order is
 * unchanged; the only edit is that what used to read `this.x` now arrives as an argument.
 * Behaviour is meant to be identical, and the specs pin the projections field by field.</p>
 */

/**
 * `appointmentId` on every child: the appointment does not exist when the payload is built,
 * and the server overwrites it after creating one. Sent explicitly rather than omitted so the
 * intent is visible at the call site -- the reasoning carried over from the component.
 */
export const UNASSIGNED_APPOINTMENT_ID = '00000000-0000-0000-0000-000000000000';

/**
 * The slice of the wizard's raw form value these functions read.
 *
 * <p>Generated from the `fb.group` declaration rather than hand-written, so the types are the
 * form's own and not a guess. Every member is optional: the caller passes the whole raw value,
 * which is wider than this, and structural typing accepts it.</p>
 */
export interface SubmitFormRawValue {
  address?: string | null;
  applicantAttorneyCity?: string | null;
  applicantAttorneyEmail?: string | null;
  applicantAttorneyEnabled?: boolean | null;
  applicantAttorneyFaxNumber?: string | null;
  applicantAttorneyFirmName?: string | null;
  applicantAttorneyFirstName?: string | null;
  applicantAttorneyIdentityUserId?: string | null;
  applicantAttorneyLastName?: string | null;
  applicantAttorneyPhoneNumber?: string | null;
  applicantAttorneyStateId?: string | null;
  applicantAttorneyStreet?: string | null;
  applicantAttorneyWebAddress?: string | null;
  applicantAttorneyZipCode?: string | null;
  appointmentClaimExaminerCity?: string | null;
  appointmentClaimExaminerEmail?: string | null;
  appointmentClaimExaminerFax?: string | null;
  appointmentClaimExaminerName?: string | null;
  appointmentClaimExaminerPhoneNumber?: string | null;
  appointmentClaimExaminerStateId?: string | null;
  appointmentClaimExaminerStreet?: string | null;
  appointmentClaimExaminerSuite?: string | null;
  appointmentClaimExaminerZip?: string | null;
  appointmentInsuranceCity?: string | null;
  appointmentInsuranceFaxNumber?: string | null;
  appointmentInsuranceName?: string | null;
  appointmentInsurancePhoneNumber?: string | null;
  appointmentInsuranceStateId?: string | null;
  appointmentInsuranceStreet?: string | null;
  appointmentInsuranceSuite?: string | null;
  appointmentInsuranceZip?: string | null;
  appointmentLanguageId?: string | null;
  cellPhoneNumber?: string | null;
  city?: string | null;
  dateOfBirth?: string | null;
  defenseAttorneyCity?: string | null;
  defenseAttorneyEmail?: string | null;
  defenseAttorneyEnabled?: boolean | null;
  defenseAttorneyFaxNumber?: string | null;
  defenseAttorneyFirmName?: string | null;
  defenseAttorneyFirstName?: string | null;
  defenseAttorneyIdentityUserId?: string | null;
  defenseAttorneyLastName?: string | null;
  defenseAttorneyPhoneNumber?: string | null;
  defenseAttorneyStateId?: string | null;
  defenseAttorneyStreet?: string | null;
  defenseAttorneyWebAddress?: string | null;
  defenseAttorneyZipCode?: string | null;
  email?: string | null;
  employerCity?: string | null;
  employerName?: string | null;
  employerOccupation?: string | null;
  employerPhoneNumber?: string | null;
  employerStateId?: string | null;
  employerStreet?: string | null;
  employerZipCode?: string | null;
  firstName?: string | null;
  genderId?: number | null;
  identityUserId?: string | null;
  interpreterVendorName?: string | null;
  lastName?: string | null;
  middleName?: string | null;
  needsInterpreter?: boolean | null;
  patientId?: string | null;
  phoneNumber?: string | null;
  phoneNumberTypeId?: number | null;
  socialSecurityNumber?: string | null;
  stateId?: string | null;
  street?: string | null;
  zipCode?: string | null;
}

/** The existing patient record, when the booking is against one. */
type ExistingPatient = NonNullable<PatientWithNavigationPropertiesDto['patient']>;

/** Master-record ids and stamps the component resolves during attorney search. */
export interface AttorneyLink {
  id?: string | null;
  concurrencyStamp?: string | null;
}

export function buildPatientCreateInput(raw: SubmitFormRawValue): AppointmentSubmitDto['patient'] {
  const dateOfBirth = formatDateOfBirthForApi(raw.dateOfBirth);
  if (!dateOfBirth) {
    throw new Error('Date of birth is required for new patient.');
  }

  return {
    firstName: raw.firstName || '',
    lastName: raw.lastName || '',
    middleName: raw.middleName ?? undefined,
    // task_d5407b22 (2026-07-21): patient email is optional. Send null (not "") when blank -- the
    // DTO's [EmailAddress] rejects an empty string but allows null.
    email: raw.email?.trim() || null,
    genderId: (raw.genderId as any) ?? undefined,
    dateOfBirth,
    phoneNumberTypeId: (raw.phoneNumberTypeId as any) ?? undefined,
    phoneNumber: raw.phoneNumber ?? undefined,
    socialSecurityNumber: raw.socialSecurityNumber ?? undefined,
    // The "Unit #" control is still NAMED `address`; its value belongs in apptNumber. Decided and
    // tested in patient-unit.mapper. There is deliberately no `address:` here -- sending it too
    // would keep the old two-column split alive.
    apptNumber: unitToDto(raw.address),
    city: raw.city ?? undefined,
    zipCode: raw.zipCode ?? undefined,
    cellPhoneNumber: raw.cellPhoneNumber ?? undefined,
    street: raw.street ?? undefined,
    interpreterVendorName: raw.needsInterpreter
      ? (raw.interpreterVendorName ?? undefined)
      : undefined,
    stateId: raw.stateId ?? undefined,
    appointmentLanguageId: raw.appointmentLanguageId ?? undefined,
  };
}

/**
 * Field-for-field port of the old updatePatientProfile body. The endpoint choice it used to make
 * (`/patients/me` for a Patient-role booker, `/patients/for-appointment-booking/{id}` for everyone
 * else) is now the SERVER's decision, derived from whether the record's login is the caller -- a
 * client-supplied choice would let a patient aim self-service overwrite semantics at someone
 * else's record.
 */
export function buildPatientUpdateInput(
  raw: SubmitFormRawValue,
  existing: ExistingPatient,
): AppointmentSubmitDto['patientUpdate'] {
  const needsInterpreter = raw.needsInterpreter === true || `${raw.needsInterpreter}` === 'true';

  return {
    firstName: raw.firstName || '',
    lastName: raw.lastName || '',
    middleName: raw.middleName ?? undefined,
    email: raw.email || '',
    genderId: (raw.genderId as any) ?? undefined,
    dateOfBirth: raw.dateOfBirth ?? undefined,
    phoneNumber: raw.phoneNumber ?? undefined,
    socialSecurityNumber: raw.socialSecurityNumber ?? undefined,
    // As above: the "Unit #" control feeds apptNumber, and `address` is deliberately absent.
    apptNumber: unitToDto(raw.address),
    city: raw.city ?? undefined,
    zipCode: raw.zipCode ?? undefined,
    cellPhoneNumber: raw.cellPhoneNumber ?? undefined,
    phoneNumberTypeId: (raw.phoneNumberTypeId as any) ?? undefined,
    street: raw.street ?? undefined,
    interpreterVendorName: needsInterpreter ? (raw.interpreterVendorName ?? undefined) : undefined,
    othersLanguageName: existing.othersLanguageName ?? undefined,
    stateId: raw.stateId ?? undefined,
    appointmentLanguageId: raw.appointmentLanguageId ?? undefined,
    identityUserId: raw.identityUserId ?? existing.identityUserId ?? undefined,
    tenantId: existing.tenantId ?? undefined,
    // Carried so a concurrent edit is refused instead of silently clobbered. The server compares
    // it before writing anything.
    concurrencyStamp: existing.concurrencyStamp,
  };
}

export function buildSubmitPatient(
  raw: SubmitFormRawValue,
  existing: ExistingPatient | undefined,
): Pick<AppointmentSubmitDto, 'patientId' | 'patient' | 'patientUpdate'> {
  const patientId = raw.patientId || undefined;

  return {
    patientId,
    patient: patientId ? undefined : buildPatientCreateInput(raw),
    // Only an existing record can be updated -- mirrors the old updatePatientProfile, which
    // returned early when there was no id.
    patientUpdate: existing?.id ? buildPatientUpdateInput(raw, existing) : undefined,
  };
}

/** True when the booker entered anything at all in the employer block. */
export function hasEmployerDetails(raw: SubmitFormRawValue): boolean {
  return !!(
    raw.employerName ||
    raw.employerOccupation ||
    raw.employerPhoneNumber ||
    raw.employerStreet ||
    raw.employerCity ||
    raw.employerStateId ||
    raw.employerZipCode
  );
}

/**
 * Port of the old employer-details POST. Same guards, same fields -- it returns the body instead
 * of sending it.
 */
export function buildSubmitEmployerDetail(
  raw: SubmitFormRawValue,
): AppointmentSubmitDto['employerDetail'] {
  if (!hasEmployerDetails(raw) || !raw.employerName || !raw.employerOccupation) {
    return undefined;
  }

  return {
    appointmentId: UNASSIGNED_APPOINTMENT_ID,
    employerName: raw.employerName,
    occupation: raw.employerOccupation,
    phoneNumber: raw.employerPhoneNumber ?? undefined,
    street: raw.employerStreet ?? undefined,
    city: raw.employerCity ?? undefined,
    stateId: raw.employerStateId ?? undefined,
    zipCode: raw.employerZipCode ?? undefined,
  };
}

export function buildSubmitApplicantAttorney(
  raw: SubmitFormRawValue,
  link: AttorneyLink,
): AppointmentSubmitDto['applicantAttorney'] {
  if (!raw.applicantAttorneyEnabled || !raw.applicantAttorneyEmail) {
    return undefined;
  }

  return {
    applicantAttorneyId: link.id ?? undefined,
    // Guid.Empty so the backend's ResolveIdentityUserIdForBookingAsync helper falls through to the
    // email-based lookup when no existing IdentityUser was matched at search time.
    identityUserId: raw.applicantAttorneyIdentityUserId ?? UNASSIGNED_APPOINTMENT_ID,
    firstName: raw.applicantAttorneyFirstName ?? '',
    lastName: raw.applicantAttorneyLastName ?? '',
    email: raw.applicantAttorneyEmail ?? '',
    firmName: raw.applicantAttorneyFirmName ?? undefined,
    webAddress: raw.applicantAttorneyWebAddress ?? undefined,
    phoneNumber: raw.applicantAttorneyPhoneNumber ?? undefined,
    faxNumber: raw.applicantAttorneyFaxNumber ?? undefined,
    street: raw.applicantAttorneyStreet ?? undefined,
    city: raw.applicantAttorneyCity ?? undefined,
    stateId: raw.applicantAttorneyStateId ?? undefined,
    zipCode: raw.applicantAttorneyZipCode ?? undefined,
    concurrencyStamp: link.concurrencyStamp ?? undefined,
  };
}

export function buildSubmitDefenseAttorney(
  raw: SubmitFormRawValue,
  link: AttorneyLink,
): AppointmentSubmitDto['defenseAttorney'] {
  if (!raw.defenseAttorneyEnabled || !raw.defenseAttorneyEmail) {
    return undefined;
  }

  return {
    defenseAttorneyId: link.id ?? undefined,
    identityUserId: raw.defenseAttorneyIdentityUserId ?? UNASSIGNED_APPOINTMENT_ID,
    firstName: raw.defenseAttorneyFirstName ?? '',
    lastName: raw.defenseAttorneyLastName ?? '',
    email: raw.defenseAttorneyEmail ?? '',
    firmName: raw.defenseAttorneyFirmName ?? undefined,
    webAddress: raw.defenseAttorneyWebAddress ?? undefined,
    phoneNumber: raw.defenseAttorneyPhoneNumber ?? undefined,
    faxNumber: raw.defenseAttorneyFaxNumber ?? undefined,
    street: raw.defenseAttorneyStreet ?? undefined,
    city: raw.defenseAttorneyCity ?? undefined,
    stateId: raw.defenseAttorneyStateId ?? undefined,
    zipCode: raw.defenseAttorneyZipCode ?? undefined,
    concurrencyStamp: link.concurrencyStamp ?? undefined,
  };
}

export function buildSubmitClaimExaminer(
  raw: SubmitFormRawValue,
): AppointmentSubmitDto['claimExaminer'] {
  return {
    appointmentId: UNASSIGNED_APPOINTMENT_ID,
    isActive: true,
    name: raw.appointmentClaimExaminerName,
    email: raw.appointmentClaimExaminerEmail,
    suite: raw.appointmentClaimExaminerSuite,
    phoneNumber: raw.appointmentClaimExaminerPhoneNumber,
    fax: raw.appointmentClaimExaminerFax,
    street: raw.appointmentClaimExaminerStreet,
    city: raw.appointmentClaimExaminerCity,
    zip: raw.appointmentClaimExaminerZip,
    stateId: raw.appointmentClaimExaminerStateId,
  };
}

/**
 * Port of the old primary-insurance POST. CI1 (2026-06-05): one optional Primary Insurance per
 * appointment, sent only when a company name was actually entered.
 */
export function buildSubmitPrimaryInsurance(
  raw: SubmitFormRawValue,
): AppointmentSubmitDto['primaryInsurance'] {
  if (!(raw.appointmentInsuranceName ?? '').trim()) {
    return undefined;
  }

  return {
    appointmentId: UNASSIGNED_APPOINTMENT_ID,
    isActive: true,
    name: raw.appointmentInsuranceName,
    suite: raw.appointmentInsuranceSuite,
    phoneNumber: raw.appointmentInsurancePhoneNumber,
    faxNumber: raw.appointmentInsuranceFaxNumber,
    street: raw.appointmentInsuranceStreet,
    city: raw.appointmentInsuranceCity,
    zip: raw.appointmentInsuranceZip,
    stateId: raw.appointmentInsuranceStateId,
  };
}

export function buildSubmitAccessors(
  authorizedUsers: readonly AppointmentAuthorizedUserDraft[],
): AppointmentSubmitDto['accessors'] {
  return authorizedUsers.map((item) => ({
    appointmentId: UNASSIGNED_APPOINTMENT_ID,
    email: item.email,
    firstName: item.firstName || undefined,
    lastName: item.lastName || undefined,
    role: item.userRole,
    accessTypeId: item.accessTypeId,
  }));
}

export function buildSubmitInjuryDetails(
  injuryDrafts: readonly AppointmentInjuryDraft[],
): AppointmentSubmitDto['injuryDetails'] {
  return injuryDrafts.map(
    (draft) =>
      ({
        injury: {
          appointmentId: UNASSIGNED_APPOINTMENT_ID,
          dateOfInjury: draft.dateOfInjury,
          toDateOfInjury: draft.toDateOfInjury,
          claimNumber: draft.claimNumber,
          isCumulativeInjury: draft.isCumulativeInjury,
          wcabAdj: draft.wcabAdj,
          // OBS-41 (2026-05-27): the derived comma-join is still sent alongside the structured
          // rows so legacy readers (view fallback, repo filter-text) keep working.
          bodyPartsSummary: draft.bodyPartsSummary,
          wcabOfficeId: draft.wcabOfficeId,
        },
        bodyParts: (draft.bodyParts ?? [])
          .map((description) => (description ?? '').trim())
          .filter((description) => !!description)
          .map((description) => ({
            appointmentInjuryDetailId: UNASSIGNED_APPOINTMENT_ID,
            bodyPartDescription: description,
          })),
      }) as AppointmentInjurySubmitDto,
  );
}
