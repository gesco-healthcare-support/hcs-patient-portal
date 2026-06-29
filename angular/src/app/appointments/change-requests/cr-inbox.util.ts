import { ChangeRequestConsentStatus } from '../../proxy/appointment-change-requests/change-request-consent-status.enum';
import { ChangeRequestSide } from '../../proxy/appointment-change-requests/change-request-side.enum';

/**
 * Pure helpers for the change-request inbox (Prompt 13). Age + consent + side
 * presentation is derived client-side from the request DTO so the inbox needs no
 * extra server call. Standalone functions (not component methods) so they
 * unit-test without a TestBed, mirroring the other *-inbox / *-detail utils.
 */

export type CrAgeClass = 'ok' | 'warn' | 'crit';

/** Whole days a request has waited since it was filed. Clamped at 0; 0 for a bad date. */
export function changeRequestAgeDays(
  createdTime: string | Date | null | undefined,
  nowMs: number,
): number {
  if (!createdTime) {
    return 0;
  }
  const created = new Date(createdTime).getTime();
  if (Number.isNaN(created)) {
    return 0;
  }
  const days = Math.floor((nowMs - created) / 86_400_000);
  return days < 0 ? 0 : days;
}

/** Age-pill bucket (prototype rule): >=7 crit, >=4 warn, else ok. */
export function changeRequestAgeClass(days: number): CrAgeClass {
  if (days >= 7) {
    return 'crit';
  }
  if (days >= 4) {
    return 'warn';
  }
  return 'ok';
}

export interface CrConsentView {
  /** Whether to render the consent chip at all (hidden when consent is not in play). */
  show: boolean;
  label: string;
  /** CSS modifier on .cr-consent: pending | agreed | declined. */
  cls: string;
}

/**
 * Consent chip. Only shown when opposing-side consent is actually in play --
 * NotRequired (consent gating off / no opposing party) renders nothing.
 */
export function changeRequestConsentView(
  status: ChangeRequestConsentStatus | null | undefined,
): CrConsentView {
  switch (status) {
    case ChangeRequestConsentStatus.Approved:
      return { show: true, label: 'Consent received', cls: 'agreed' };
    case ChangeRequestConsentStatus.Pending:
      return { show: true, label: 'Consent pending', cls: 'pending' };
    case ChangeRequestConsentStatus.Rejected:
      return { show: true, label: 'Consent declined', cls: 'declined' };
    case ChangeRequestConsentStatus.Expired:
      return { show: true, label: 'Consent expired', cls: 'declined' };
    default:
      return { show: false, label: '', cls: '' };
  }
}

/**
 * True when consent is in a state that BLOCKS approval. The server
 * (OpposingConsentValidator) only allows approval for Approved / NotRequired;
 * Pending, Rejected and Expired are all blocked -- there is NO override path, so
 * the UI must not let staff finalize these (it would 403). Reject is unaffected.
 */
export function consentBlocksApproval(
  status: ChangeRequestConsentStatus | null | undefined,
): boolean {
  return (
    status === ChangeRequestConsentStatus.Pending ||
    status === ChangeRequestConsentStatus.Rejected ||
    status === ChangeRequestConsentStatus.Expired
  );
}

/**
 * Corrective note shown in the approve modal when consent blocks approval. There
 * is no "override" -- the only way forward is to Reject the request (which has no
 * consent gate) or wait for consent. Null when approval is allowed (Approved /
 * NotRequired).
 */
export function consentBlockNote(
  status: ChangeRequestConsentStatus | null | undefined,
): string | null {
  switch (status) {
    case ChangeRequestConsentStatus.Pending:
      return "The opposing party's consent is still pending, so this cannot be approved yet. Reject the request instead, or wait for their consent.";
    case ChangeRequestConsentStatus.Rejected:
      return 'The opposing party declined consent, so this cannot be approved. Reject the request instead.';
    case ChangeRequestConsentStatus.Expired:
      return "The opposing party's consent window expired, so this cannot be approved. Reject the request instead.";
    default:
      return null;
  }
}

/** Which side filed the request, for the inbox row. Empty when unknown. */
export function requestingSideLabel(side: ChangeRequestSide | null | undefined): string {
  switch (side) {
    case ChangeRequestSide.SideA:
      return 'Applicant side';
    case ChangeRequestSide.SideB:
      return 'Defense side';
    default:
      return '';
  }
}
