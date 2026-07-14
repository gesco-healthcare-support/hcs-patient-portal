/** The four sections of the People hub. */
export type PeopleSectionKey = 'patients' | 'aa' | 'da' | 'ce';

/** Portal-account state shown as a chip. */
export type PortalStatus = 'linked' | 'invited' | 'none';

export const PORTAL_LABEL: Record<PortalStatus, string> = {
  linked: 'Linked',
  invited: 'Invited',
  none: 'No account',
};

/**
 * Static metadata for one People section: rail label/icon/route + gating policy,
 * the shape flags the table/modal switch on, and the ExternalUserType used for
 * the Invite-to-portal deep-link query.
 */
export interface PeopleSection {
  key: PeopleSectionKey;
  label: string;
  singular: string;
  icon: string;
  route: string;
  policy: string;
  /** Patients get the detail view, column chooser, filters, and demographics. */
  isPatient: boolean;
  /** Applicant/Defense attorneys have a Firm column + modal field. */
  hasFirm: boolean;
  /** ExternalUserType name for the /users/invite deep-link. */
  inviteUserType: string;
}

export const PEOPLE_SECTIONS: PeopleSection[] = [
  {
    key: 'patients',
    label: 'Patients',
    singular: 'patient',
    icon: 'users',
    route: '/user-management/patients',
    policy: 'CaseEvaluation.Patients',
    isPatient: true,
    hasFirm: false,
    inviteUserType: 'Patient',
  },
  {
    key: 'aa',
    label: 'Applicant Attorneys',
    singular: 'applicant attorney',
    icon: 'user',
    route: '/applicant-attorneys',
    policy: 'CaseEvaluation.ApplicantAttorneys',
    isPatient: false,
    hasFirm: true,
    inviteUserType: 'ApplicantAttorney',
  },
  {
    key: 'da',
    label: 'Defense Attorneys',
    singular: 'defense attorney',
    icon: 'user',
    route: '/defense-attorneys',
    policy: 'CaseEvaluation.DefenseAttorneys',
    isPatient: false,
    hasFirm: true,
    inviteUserType: 'DefenseAttorney',
  },
  {
    key: 'ce',
    label: 'Claim Examiners',
    singular: 'claim examiner',
    icon: 'user',
    route: '/claim-examiners',
    policy: 'CaseEvaluation.ClaimExaminers',
    isPatient: false,
    hasFirm: false,
    inviteUserType: 'ClaimExaminer',
  },
];

/** A person row normalized across the four sections for the directory table. */
export interface PersonRow {
  id: string;
  firstName: string;
  lastName: string;
  email?: string | null;
  phoneNumber?: string | null;
  identityUserId?: string | null;
  portal: PortalStatus;
  // attorney
  firmName?: string | null;
  firmAddress?: string | null;
  webAddress?: string | null;
  faxNumber?: string | null;
  // patient demographics
  middleName?: string | null;
  genderId?: number | null;
  dateOfBirth?: string | null;
  cellPhoneNumber?: string | null;
  phoneNumberTypeId?: number | null;
  socialSecurityNumber?: string | null;
  apptNumber?: string | null;
  // address + preferences
  street?: string | null;
  city?: string | null;
  stateId?: string | null;
  stateName?: string | null;
  zipCode?: string | null;
  appointmentLanguageId?: string | null;
  languageName?: string | null;
  othersLanguageName?: string | null;
  interpreterVendorName?: string | null;
  concurrencyStamp?: string;
}

/** Create/edit modal draft, superset across sections. */
export interface PersonFormState {
  id: string | null;
  /**
   * Carried verbatim from the row so updates re-send the existing portal link
   * rather than clearing it -- the modal never edits this; it just preserves it.
   */
  identityUserId: string | null;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string;
  // patient
  middleName: string;
  genderId: number | null;
  dateOfBirth: string;
  cellPhoneNumber: string;
  phoneNumberTypeId: number | null;
  /** Write-only: blank means "leave the stored SSN unchanged" (backend preserves on empty). */
  socialSecurityNumber: string;
  street: string;
  city: string;
  stateId: string;
  zipCode: string;
  appointmentLanguageId: string;
  othersLanguageName: string;
  interpreterVendorName: string;
  apptNumber: string;
  // attorney
  firmName: string;
  firmAddress: string;
  webAddress: string;
  faxNumber: string;
  concurrencyStamp?: string;
}

/**
 * Derives the portal-account chip. Linked wins (an IdentityUser is attached);
 * otherwise Invited when the email has an active invitation; otherwise None.
 * Attorneys have no email on their DTO, so they only ever resolve to linked/none
 * -- "invited" is undeterminable without an email and is intentionally not shown.
 */
export function derivePortalStatus(
  identityUserId: string | null | undefined,
  email: string | null | undefined,
  activeInvitedEmails: ReadonlySet<string>,
): PortalStatus {
  if (identityUserId) {
    return 'linked';
  }
  if (email && activeInvitedEmails.has(email.trim().toLowerCase())) {
    return 'invited';
  }
  return 'none';
}

/** Client-side patient filter state for the directory filter panel. */
export interface PeopleFilters {
  genderId: number | null;
  dobMin: string;
  dobMax: string;
  city: string;
  stateId: string;
  languageId: string;
  portal: PortalStatus | '';
}

export function emptyPeopleFilters(): PeopleFilters {
  return {
    genderId: null,
    dobMin: '',
    dobMax: '',
    city: '',
    stateId: '',
    languageId: '',
    portal: '',
  };
}

/** True when the row satisfies every set filter (unset filters always pass). */
export function matchesPeopleFilters(row: PersonRow, filters: PeopleFilters): boolean {
  if (filters.genderId != null && row.genderId !== filters.genderId) {
    return false;
  }
  const dob = row.dateOfBirth ?? '';
  if (filters.dobMin && (!dob || dob < filters.dobMin)) {
    return false;
  }
  if (filters.dobMax && (!dob || dob > filters.dobMax)) {
    return false;
  }
  if (filters.city && !(row.city ?? '').toLowerCase().includes(filters.city.trim().toLowerCase())) {
    return false;
  }
  if (filters.stateId && row.stateId !== filters.stateId) {
    return false;
  }
  if (filters.languageId && row.appointmentLanguageId !== filters.languageId) {
    return false;
  }
  if (filters.portal && row.portal !== filters.portal) {
    return false;
  }
  return true;
}

/** Case-insensitive client-side search across name, email, phone, and firm. */
export function matchesPeopleSearch(row: PersonRow, query: string): boolean {
  const needle = query.trim().toLowerCase();
  if (!needle) {
    return true;
  }
  const haystack = [
    `${row.firstName} ${row.lastName}`,
    row.email ?? '',
    row.phoneNumber ?? '',
    row.firmName ?? '',
  ]
    .join(' ')
    .toLowerCase();
  return haystack.includes(needle);
}

/**
 * Masks a social-security value to the last four digits for display. Works
 * whether the source is already masked or full -- it only ever keeps the last
 * four digits, so a full SSN is never rendered even if the API returns one.
 * Returns '' for empty input and a fully-masked value when fewer than 4 digits.
 */
export function maskSsn(raw: string | null | undefined): string {
  if (!raw) {
    return '';
  }
  const digits = raw.replace(/\D/g, '');
  if (digits.length < 4) {
    return '***-**-****';
  }
  return `***-**-${digits.slice(-4)}`;
}

/** Up to two uppercase initials for an avatar chip; '?' when the name is blank. */
export function initials(firstName: string, lastName: string): string {
  const first = (firstName ?? '').trim().charAt(0);
  const last = (lastName ?? '').trim().charAt(0);
  return (first + last).toUpperCase() || '?';
}

/** Deterministic avatar background derived from a seed string (no randomness). */
export function avatarColor(seed: string): string {
  let hash = 0;
  for (let i = 0; i < seed.length; i++) {
    hash = (hash * 31 + seed.charCodeAt(i)) | 0;
  }
  return `hsl(${Math.abs(hash) % 360}, 55%, 45%)`;
}
