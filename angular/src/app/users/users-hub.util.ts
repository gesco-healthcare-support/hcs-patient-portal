import { ExternalUserType } from '../proxy/external-signups/external-user-type.enum';
import { InvitationStatus } from '../proxy/invitations/invitation-status.enum';

/** The four sections of the Users & Access hub, in rail order. */
export type UsersSectionKey = 'invite' | 'pending' | 'staff' | 'tenants';

/** Rail metadata for one section: label, icon, deep-link route, and gating policy. */
export interface UsersSection {
  key: UsersSectionKey;
  label: string;
  /** IconComponent name. */
  icon: string;
  /** Absolute router path the rail item links to (deep-linkable + route-guarded). */
  route: string;
  /** ABP policy gating the rail item + its route. */
  policy: string;
}

/**
 * Section rail. Visibility keys off the granted policy (mirrors the Configuration
 * hub): invite + pending need InviteExternalUser (all internal roles); Internal
 * Users needs InternalUsers.Create (Supervisor + IT Admin); Tenants needs
 * Saas.Tenants (IT Admin / host). So the rail renders exactly the role matrix
 * without a separate role check.
 */
export const USERS_SECTIONS: UsersSection[] = [
  {
    key: 'invite',
    label: 'Invite External User',
    icon: 'user',
    route: '/users/invite',
    policy: 'CaseEvaluation.UserManagement.InviteExternalUser',
  },
  {
    key: 'pending',
    label: 'Pending Invites',
    icon: 'clock',
    route: '/users/pending',
    policy: 'CaseEvaluation.UserManagement.InviteExternalUser',
  },
  {
    key: 'staff',
    label: 'Internal Users',
    icon: 'users',
    route: '/users/internal',
    policy: 'CaseEvaluation.InternalUsers.Create',
  },
  {
    key: 'tenants',
    label: 'Tenants',
    icon: 'grid',
    route: '/users/tenants',
    policy: 'Saas.Tenants',
  },
];

/** Role names that count as INTERNAL staff -- the Internal Users list filters to these. */
export const INTERNAL_ROLE_NAMES = ['IT Admin', 'Staff Supervisor', 'Intake Staff'];

/** A selectable external role on the invite form. */
export interface InviteRoleOption {
  value: ExternalUserType;
  label: string;
}

/** External roles that can be invited (the invite-form select). */
export const INVITE_ROLE_OPTIONS: InviteRoleOption[] = [
  { value: ExternalUserType.Patient, label: 'Patient' },
  { value: ExternalUserType.ApplicantAttorney, label: 'Applicant Attorney' },
  { value: ExternalUserType.DefenseAttorney, label: 'Defense Attorney' },
  { value: ExternalUserType.ClaimExaminer, label: 'Claim Examiner' },
];

/** Internal roles an admin may create (the create-internal-user modal select). */
export const CREATABLE_INTERNAL_ROLES = ['Staff Supervisor', 'Intake Staff'];

/**
 * Maps the People-hub deep-link `userType` query string to the numeric
 * ExternalUserType the invite form binds to. Unknown/blank defaults to Patient.
 */
export function userTypeFromName(name: string | null | undefined): ExternalUserType {
  switch (name) {
    case 'ClaimExaminer':
      return ExternalUserType.ClaimExaminer;
    case 'ApplicantAttorney':
      return ExternalUserType.ApplicantAttorney;
    case 'DefenseAttorney':
      return ExternalUserType.DefenseAttorney;
    case 'Patient':
    default:
      return ExternalUserType.Patient;
  }
}

/** True for the two attorney roles -- the invite form shows the firm-name field for them. */
export function isAttorneyType(type: ExternalUserType): boolean {
  return type === ExternalUserType.ApplicantAttorney || type === ExternalUserType.DefenseAttorney;
}

/** Expiry countdown chip for the Pending Invites table. */
export interface ExpiryChip {
  cls: 'ok' | 'soon' | 'gone';
  label: string;
}

/**
 * Days-to-expiry chip. Only a still-Pending invite shows a countdown; an accepted,
 * expired, or revoked row collapses to a dash ('gone'). `nowMs` is passed in so the
 * function stays pure + unit-testable.
 */
export function expiryChip(
  expiresAtIso: string | null | undefined,
  status: InvitationStatus,
  nowMs: number,
): ExpiryChip {
  if (status !== InvitationStatus.Pending || !expiresAtIso) {
    return { cls: 'gone', label: '--' };
  }
  const remainingMs = new Date(expiresAtIso).getTime() - nowMs;
  const days = Math.ceil(remainingMs / 86400000);
  if (days <= 0) {
    return { cls: 'gone', label: '--' };
  }
  if (days <= 1) {
    return { cls: 'soon', label: '1 day' };
  }
  return { cls: 'ok', label: days + ' days' };
}

/** Invitation status dot-chip (pp-portal family). */
export interface StatusChip {
  cls: 'invited' | 'linked' | 'none';
  label: string;
}

export function invitationStatusChip(status: InvitationStatus): StatusChip {
  switch (status) {
    case InvitationStatus.Accepted:
      return { cls: 'linked', label: 'Accepted' };
    case InvitationStatus.Expired:
      return { cls: 'none', label: 'Expired' };
    case InvitationStatus.Revoked:
      return { cls: 'none', label: 'Revoked' };
    case InvitationStatus.Pending:
    default:
      return { cls: 'invited', label: 'Pending' };
  }
}

/** Role chip variant for the internal-users table (Supervisor = purple, IT Admin = green). */
export function roleChipClass(roleName: string | null | undefined): string {
  if (roleName === 'Staff Supervisor') {
    return 'sup';
  }
  if (roleName === 'IT Admin') {
    return 'admin';
  }
  return '';
}

/** The first internal role on a user (the chip + sort key), else a dash. */
export function primaryInternalRole(roleNames: string[] | null | undefined): string {
  const names = roleNames ?? [];
  for (const candidate of INTERNAL_ROLE_NAMES) {
    if (names.includes(candidate)) {
      return candidate;
    }
  }
  return names[0] ?? '--';
}

/** Two-letter initials for the avatar; falls back to the email's first letter. */
export function initials(
  first?: string | null,
  last?: string | null,
  email?: string | null,
): string {
  const f = (first ?? '').trim();
  const l = (last ?? '').trim();
  if (f || l) {
    return ((f.charAt(0) || '') + (l.charAt(0) || '')).toUpperCase() || '?';
  }
  const e = (email ?? '').trim();
  return (e.charAt(0) || '?').toUpperCase();
}

const AVATAR_COLORS = [
  '#055495',
  '#0b7285',
  '#5f3dc4',
  '#9c36b5',
  '#a61e4d',
  '#2b8a3e',
  '#b8860b',
  '#c2410c',
];

/** Deterministic avatar background from a seed string (stable per user). */
export function avatarColor(seed: string | null | undefined): string {
  const value = seed ?? '';
  let hash = 0;
  for (let i = 0; i < value.length; i++) {
    hash = (hash * 31 + value.charCodeAt(i)) | 0;
  }
  return AVATAR_COLORS[Math.abs(hash) % AVATAR_COLORS.length];
}
