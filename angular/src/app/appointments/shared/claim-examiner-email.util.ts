import { isPlaceholderEmail } from '../../shared/placeholder-email';

/**
 * 2026-11-13: when the claim examiner EMAIL may be left blank in the booking wizard.
 *
 * Only a law-office booker (applicant / defense attorney or paralegal, never staff) who has also given
 * the OTHER side's attorney a real email: that attorney is then the other side's contact. The server
 * enforces the same rule from the caller's roles; this only decides whether the form asks for it.
 * Name, phone and address stay required either way.
 */
export interface OtherSideAttorney {
  readonly enabled: boolean;
  readonly email: string | null | undefined;
}

const EMAIL_SHAPE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function isRealEmail(value: string | null | undefined): boolean {
  const trimmed = (value ?? '').trim();
  return EMAIL_SHAPE.test(trimmed) && !isPlaceholderEmail(trimmed);
}

export function claimExaminerEmailMayBeBlank(
  bookerSide: 'applicant' | 'defense' | null,
  otherSideAttorney: OtherSideAttorney,
): boolean {
  if (bookerSide === null) {
    return false;
  }
  return otherSideAttorney.enabled && isRealEmail(otherSideAttorney.email);
}
