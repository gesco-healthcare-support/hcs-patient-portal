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

/** A side needs nothing (NotRequired) or has already approved -> satisfied for the finalize gate. */
function sideSatisfied(status: ChangeRequestConsentStatus | null | undefined): boolean {
  return (
    status === undefined ||
    status === null ||
    status === ChangeRequestConsentStatus.NotRequired ||
    status === ChangeRequestConsentStatus.Approved
  );
}

/** A side is "in play" (its consent was actually solicited) when it is anything but NotRequired. */
function sideInPlay(status: ChangeRequestConsentStatus | null | undefined): boolean {
  return (
    status !== undefined && status !== null && status !== ChangeRequestConsentStatus.NotRequired
  );
}

/**
 * Consent chip for the two-sided model (2026-07-01). Aggregates the two side
 * slots (Side A = applicant, Side B = defense): hidden when neither side is in
 * play (gating off / no reps); "received" only when every in-play side is
 * Approved; "declined"/"expired" if any side rejected/expired; else "pending".
 */
export function changeRequestConsentView(
  sideA: ChangeRequestConsentStatus | null | undefined,
  sideB: ChangeRequestConsentStatus | null | undefined,
): CrConsentView {
  if (!sideInPlay(sideA) && !sideInPlay(sideB)) {
    return { show: false, label: '', cls: '' };
  }
  const inPlay = [sideA, sideB].filter(sideInPlay);
  if (inPlay.some((s) => s === ChangeRequestConsentStatus.Rejected)) {
    return { show: true, label: 'Consent declined', cls: 'declined' };
  }
  if (inPlay.some((s) => s === ChangeRequestConsentStatus.Expired)) {
    return { show: true, label: 'Consent expired', cls: 'declined' };
  }
  if (inPlay.every((s) => s === ChangeRequestConsentStatus.Approved)) {
    return { show: true, label: 'Consent received', cls: 'agreed' };
  }
  return { show: true, label: 'Consent pending', cls: 'pending' };
}

/**
 * True when consent BLOCKS approval. The finalize gate (server
 * OpposingConsentValidator -> AreAllRequiredSidesGranted) passes only when EVERY
 * side is satisfied (NotRequired or Approved); any Pending / Rejected / Expired
 * side blocks. There is NO override path, so the UI must not let staff finalize
 * (it would 403). Reject is unaffected.
 */
export function consentBlocksApproval(
  sideA: ChangeRequestConsentStatus | null | undefined,
  sideB: ChangeRequestConsentStatus | null | undefined,
): boolean {
  return !(sideSatisfied(sideA) && sideSatisfied(sideB));
}

/**
 * Corrective note shown in the approve modal when consent blocks approval. There
 * is no "override" -- the only way forward is to Reject the request (which has no
 * consent gate) or wait for consent. Null when approval is allowed (every side
 * Approved / NotRequired).
 */
export function consentBlockNote(
  sideA: ChangeRequestConsentStatus | null | undefined,
  sideB: ChangeRequestConsentStatus | null | undefined,
): string | null {
  if (!consentBlocksApproval(sideA, sideB)) {
    return null;
  }
  if (
    sideA === ChangeRequestConsentStatus.Rejected ||
    sideB === ChangeRequestConsentStatus.Rejected
  ) {
    return 'A party declined consent, so this cannot be approved. Reject the request instead.';
  }
  if (
    sideA === ChangeRequestConsentStatus.Expired ||
    sideB === ChangeRequestConsentStatus.Expired
  ) {
    return "A party's consent window expired, so this cannot be approved. Reject the request instead.";
  }
  return 'Both parties must consent before this can be approved, and at least one is still pending. Reject the request instead, or wait for their consent.';
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
