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
 * Warning shown in the approve modal when finalizing would override an
 * unresolved opposing-side consent. Null when consent is Approved or NotRequired
 * (nothing to override).
 */
export function consentOverrideWarning(
  status: ChangeRequestConsentStatus | null | undefined,
): string | null {
  switch (status) {
    case ChangeRequestConsentStatus.Pending:
      return 'Opposing-counsel consent is still pending -- approving now overrides it.';
    case ChangeRequestConsentStatus.Rejected:
      return 'Opposing-counsel consent was declined -- approving now overrides it.';
    case ChangeRequestConsentStatus.Expired:
      return 'Opposing-counsel consent expired -- approving now overrides it.';
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
