import type {
  AppointmentDto,
  ApplicantAttorneyDetailsDto,
  DefenseAttorneyDetailsDto,
} from '../../proxy/appointments/models';
import type { AppointmentEmployerDetailDto } from '../../proxy/appointment-employer-details/models';
import type { AppointmentInjuryDetailWithNavigationPropertiesDto } from '../../proxy/appointment-injury-details/models';
import type { AppointmentAccessorWithNavigationPropertiesDto } from '../../proxy/appointment-accessors/models';
import type { AppointmentClaimExaminerDto } from '../../proxy/appointment-claim-examiners/models';
import type { AppointmentPrimaryInsuranceDto } from '../../proxy/appointment-primary-insurances/models';
import type { AppointmentInjuryDraft } from '../sections/appointment-add-claim-information.component';
import type {
  AppointmentAuthorizedUserDraft,
  ExternalAuthorizedUserOption,
} from '../sections/appointment-add-authorized-users.component';

/**
 * G-01-07 -- pure DTO -> booking-form mapping for the reval (Re-evaluation)
 * and re-request flows. The NEW analogue of OLD's `bindFormGroup`
 * Revel/ReRequest branch (appointment.domain.ts:325-417): copy the prior
 * intake forward while resetting every child PK to 0.
 *
 * "Reset PKs" is implicit here: the produced injury / authorized-user drafts
 * carry NO entity ids, so the existing submit cascade
 * (persistInjuryDraftsIfProvided / createAppointmentAccessorsIfProvided)
 * inserts fresh child rows against the newly-created appointment. The patient
 * row is reused (it is a standalone entity, not a child of the appointment),
 * and the attorney entity ids ARE carried so the upsert reuses the same
 * ApplicantAttorney / DefenseAttorney record and only creates a new join.
 *
 * Custom-field VALUES are intentionally NOT prefilled (decision 2026-06-02):
 * the read DTO does not carry them and there is no read path yet. Tracked as
 * a follow-up.
 */
export interface RevalPrefillSources {
  appointment: AppointmentDto;
  employer: AppointmentEmployerDetailDto | null;
  applicantAttorney: ApplicantAttorneyDetailsDto | null;
  defenseAttorney: DefenseAttorneyDetailsDto | null;
  injuries: AppointmentInjuryDetailWithNavigationPropertiesDto[];
  accessors: AppointmentAccessorWithNavigationPropertiesDto[];
  authorizedUserOptions: ExternalAuthorizedUserOption[];
  // #296: Claim Examiner + Primary Insurance are one-per-appointment now, so
  // they arrive from the source appointment graph (not per injury).
  claimExaminer: AppointmentClaimExaminerDto | null;
  primaryInsurance: AppointmentPrimaryInsuranceDto | null;
}

export interface RevalPrefillResult {
  /** Patch for the main FormGroup (employer + attorney fields + panel/due). */
  formPatch: Record<string, unknown>;
  injuryDrafts: AppointmentInjuryDraft[];
  authorizedUsers: AppointmentAuthorizedUserDraft[];
  /** Carried so the attorney upsert reuses the existing entity (new join only). */
  applicantAttorneyId: string | null;
  applicantAttorneyConcurrencyStamp: string | null;
  defenseAttorneyId: string | null;
  defenseAttorneyConcurrencyStamp: string | null;
}

/** Employer detail row -> employer* form controls. */
function mapEmployerToPatch(e: AppointmentEmployerDetailDto | null): Record<string, unknown> {
  if (!e) return {};
  return {
    employerName: e.employerName ?? null,
    employerOccupation: e.occupation ?? null,
    employerPhoneNumber: e.phoneNumber ?? null,
    employerStreet: e.street ?? null,
    employerCity: e.city ?? null,
    employerStateId: e.stateId ?? null,
    employerZipCode: e.zipCode ?? null,
  };
}

/**
 * Attorney details -> prefixed form controls (no Enabled flag -- the parent
 * sets that, plus validators, via applyAttorneyEnabledFromSource so an absent
 * attorney does not leave a required-but-empty section blocking submit).
 */
function mapAttorneyFieldsToPatch(
  data: ApplicantAttorneyDetailsDto | DefenseAttorneyDetailsDto | null,
  prefix: 'applicantAttorney' | 'defenseAttorney',
): Record<string, unknown> {
  if (!data) return {};
  return {
    [`${prefix}IdentityUserId`]: data.identityUserId ?? null,
    [`${prefix}FirstName`]: data.firstName ?? null,
    [`${prefix}LastName`]: data.lastName ?? null,
    [`${prefix}Email`]: data.email ?? null,
    [`${prefix}FirmName`]: data.firmName ?? null,
    [`${prefix}WebAddress`]: data.webAddress ?? null,
    [`${prefix}PhoneNumber`]: data.phoneNumber ?? null,
    [`${prefix}FaxNumber`]: data.faxNumber ?? null,
    [`${prefix}Street`]: data.street ?? null,
    [`${prefix}City`]: data.city ?? null,
    [`${prefix}StateId`]: data.stateId ?? null,
    [`${prefix}ZipCode`]: data.zipCode ?? null,
  };
}

/**
 * Injury nav-props row -> draft. #296 moved Claim Examiner + Primary Insurance
 * off the per-injury graph to one-per-appointment, so they are no longer mapped
 * here; see mapClaimExaminerToPatch / mapInsuranceToPatch for the appointment-
 * level copy-forward.
 */
function mapInjuryToDraft(
  item: AppointmentInjuryDetailWithNavigationPropertiesDto,
): AppointmentInjuryDraft {
  const d = item.appointmentInjuryDetail;
  const bodyParts = (item.bodyParts ?? [])
    .map((b) => (b?.bodyPartDescription ?? '').trim())
    .filter(Boolean);
  return {
    isCumulativeInjury: !!d?.isCumulativeInjury,
    dateOfInjury: d?.dateOfInjury ?? null,
    toDateOfInjury: d?.toDateOfInjury ?? null,
    claimNumber: d?.claimNumber ?? '',
    wcabOfficeId: d?.wcabOfficeId ?? null,
    wcabAdj: d?.wcabAdj ?? null,
    bodyParts,
    bodyPartsSummary: (d?.bodyPartsSummary ?? bodyParts.join(', ')).slice(0, 500),
  };
}

/**
 * #296 -- copy the source appointment's Claim Examiner into the appointment-
 * level booking controls so reval / re-request carry it forward (mirrors the
 * OLD bindFormGroup copy of the examiner block).
 */
function mapClaimExaminerToPatch(ce: AppointmentClaimExaminerDto | null): Record<string, unknown> {
  if (!ce) return {};
  return {
    appointmentClaimExaminerName: ce.name ?? null,
    appointmentClaimExaminerEmail: ce.email ?? null,
    appointmentClaimExaminerSuite: ce.suite ?? null,
    appointmentClaimExaminerPhoneNumber: ce.phoneNumber ?? null,
    appointmentClaimExaminerFax: ce.fax ?? null,
    appointmentClaimExaminerStreet: ce.street ?? null,
    appointmentClaimExaminerCity: ce.city ?? null,
    appointmentClaimExaminerStateId: ce.stateId ?? null,
    appointmentClaimExaminerZip: ce.zip ?? null,
  };
}

/** #296 -- same copy-forward for the appointment's Primary Insurance block. */
function mapInsuranceToPatch(ins: AppointmentPrimaryInsuranceDto | null): Record<string, unknown> {
  if (!ins) return {};
  return {
    appointmentInsuranceName: ins.name ?? null,
    appointmentInsuranceSuite: ins.suite ?? null,
    appointmentInsurancePhoneNumber: ins.phoneNumber ?? null,
    appointmentInsuranceFaxNumber: ins.faxNumber ?? null,
    appointmentInsuranceStreet: ins.street ?? null,
    appointmentInsuranceCity: ins.city ?? null,
    appointmentInsuranceStateId: ins.stateId ?? null,
    appointmentInsuranceZip: ins.zip ?? null,
  };
}

/**
 * Accessor join row -> authorized-user draft. Name/email come from the
 * embedded IdentityUser; userRole is resolved from the lookup options (the
 * accessor row does not carry it). Drafts carry NO id so the cascade inserts
 * a fresh accessor row.
 */
function mapAccessorToDraft(
  item: AppointmentAccessorWithNavigationPropertiesDto,
  options: ExternalAuthorizedUserOption[],
): AppointmentAuthorizedUserDraft | null {
  const accessor = item.appointmentAccessor;
  const identityUserId = accessor?.identityUserId ?? '';
  if (!identityUserId) return null;
  const opt = options.find((o) => o.identityUserId === identityUserId);
  const user = item.identityUser;
  return {
    identityUserId,
    firstName: user?.name ?? opt?.firstName ?? '',
    lastName: user?.surname ?? opt?.lastName ?? '',
    email: user?.email ?? opt?.email ?? '',
    userRole: opt?.userRole ?? '',
    accessTypeId: Number(accessor?.accessTypeId ?? 23),
  };
}

/** Assemble the full prefill from the fetched source pieces. */
export function buildRevalPrefill(sources: RevalPrefillSources): RevalPrefillResult {
  const { appointment, employer, applicantAttorney, defenseAttorney } = sources;
  return {
    formPatch: {
      panelNumber: appointment.panelNumber ?? null,
      dueDate: appointment.dueDate ?? null,
      ...mapEmployerToPatch(employer),
      ...mapAttorneyFieldsToPatch(applicantAttorney, 'applicantAttorney'),
      ...mapAttorneyFieldsToPatch(defenseAttorney, 'defenseAttorney'),
      ...mapClaimExaminerToPatch(sources.claimExaminer),
      ...mapInsuranceToPatch(sources.primaryInsurance),
    },
    injuryDrafts: (sources.injuries ?? []).map(mapInjuryToDraft),
    authorizedUsers: (sources.accessors ?? [])
      .map((a) => mapAccessorToDraft(a, sources.authorizedUserOptions ?? []))
      .filter((d): d is AppointmentAuthorizedUserDraft => d !== null),
    applicantAttorneyId: applicantAttorney?.applicantAttorneyId ?? null,
    applicantAttorneyConcurrencyStamp: applicantAttorney?.concurrencyStamp ?? null,
    defenseAttorneyId: defenseAttorney?.defenseAttorneyId ?? null,
    defenseAttorneyConcurrencyStamp: defenseAttorney?.concurrencyStamp ?? null,
  };
}
